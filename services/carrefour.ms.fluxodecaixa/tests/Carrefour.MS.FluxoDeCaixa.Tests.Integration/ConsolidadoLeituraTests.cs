using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FluxoDeCaixa.Api.DTOs;
using FluxoDeCaixa.Consolidacao.Repositorios;
using FluxoDeCaixa.Domain.Eventos;
using FluxoDeCaixa.Tests.Integration.Fixtures;

namespace FluxoDeCaixa.Tests.Integration;

/// <summary>
/// Lado de LEITURA do consolidado, que é o que este serviço expõe.
///
/// O read model é populado <b>diretamente pelo repositório</b>, e não por um
/// lançamento passando pela fila: quem materializa é o worker, que não existe
/// neste repositório. Aqui validamos o que é responsabilidade do MS —
/// consultar, escopar por comerciante e responder dia sem movimento.
///
/// O caminho completo (POST → fila → consolidado) é verificado em e2e/.
/// </summary>
[Collection("Integration")]
public class ConsolidadoLeituraTests
{
    private readonly IntegrationTestFixture _fixture;

    public ConsolidadoLeituraTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private static LancamentoRegistrado Evento(Guid comercianteId, DateOnly data, decimal valor, string tipo)
        => new(
            LancamentoId: Guid.NewGuid(),
            ComercianteId: comercianteId,
            Valor: valor,
            Tipo: tipo,
            Moeda: "BRL",
            Descricao: "Semeado pelo teste",
            Categoria: "Vendas",
            Data: data,
            RegistradoEm: DateTimeOffset.UtcNow);

    [Fact]
    public async Task Deve_retornar_o_consolidado_do_comerciante_autenticado()
    {
        // Arrange — semeia o read model como o worker faria
        var repo = new PostgresConsolidadoRepository(_fixture.ConnectionString);
        await repo.CriarTabelaSeNaoExisteAsync(CancellationToken.None);

        var comercianteId = IntegrationTestFixture.IdDoComerciante(IntegrationTestFixture.ComercianteA);
        var data = DateOnly.FromDateTime(DateTime.Today.AddDays(-70));

        await repo.UpsertAsync(Evento(comercianteId, data, 300.00m, "Credito"), CancellationToken.None);
        await repo.UpsertAsync(Evento(comercianteId, data, 100.00m, "Debito"), CancellationToken.None);

        // Act
        var client = await _fixture.CriarClienteAutenticadoAsync(IntegrationTestFixture.ComercianteA);
        var resposta = await client.GetAsync($"/consolidado/diario?data={data:yyyy-MM-dd}");

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        var consolidado = await resposta.Content.ReadFromJsonAsync<ConsolidadoDiarioResponse>();
        consolidado!.TotalCreditos.Should().Be(300.00m);
        consolidado.TotalDebitos.Should().Be(100.00m);
        consolidado.SaldoLiquido.Should().Be(200.00m);
        consolidado.QuantidadeLancamentos.Should().Be(2);
    }

    [Fact]
    public async Task Consolidado_de_um_comerciante_nao_vaza_para_outro()
    {
        // Arrange — só o comerciante A tem movimento nesta data
        var repo = new PostgresConsolidadoRepository(_fixture.ConnectionString);
        await repo.CriarTabelaSeNaoExisteAsync(CancellationToken.None);

        var idA = IntegrationTestFixture.IdDoComerciante(IntegrationTestFixture.ComercianteA);
        var data = DateOnly.FromDateTime(DateTime.Today.AddDays(-71));

        await repo.UpsertAsync(Evento(idA, data, 999.99m, "Credito"), CancellationToken.None);

        // Act
        var clienteA = await _fixture.CriarClienteAutenticadoAsync(IntegrationTestFixture.ComercianteA);
        var clienteB = await _fixture.CriarClienteAutenticadoAsync(IntegrationTestFixture.ComercianteB);

        var respostaA = await clienteA.GetAsync($"/consolidado/diario?data={data:yyyy-MM-dd}");
        var respostaB = await clienteB.GetAsync($"/consolidado/diario?data={data:yyyy-MM-dd}");

        // Assert — A enxerga o próprio movimento
        var consolidadoA = await respostaA.Content.ReadFromJsonAsync<ConsolidadoDiarioResponse>();
        consolidadoA!.TotalCreditos.Should().Be(999.99m);

        // Assert — B enxerga dia zerado, nunca o valor de A
        respostaB.StatusCode.Should().Be(HttpStatusCode.OK);
        var consolidadoB = await respostaB.Content.ReadFromJsonAsync<ConsolidadoDiarioResponse>();
        consolidadoB!.TotalCreditos.Should().Be(0m);
        consolidadoB.SaldoLiquido.Should().Be(0m);
        consolidadoB.QuantidadeLancamentos.Should().Be(0);
    }

    [Fact]
    public async Task Dia_sem_movimento_responde_200_com_zeros()
    {
        var client = await _fixture.CriarClienteAutenticadoAsync(IntegrationTestFixture.ComercianteB);
        var data = DateOnly.FromDateTime(DateTime.Today.AddDays(-999));

        var resposta = await client.GetAsync($"/consolidado/diario?data={data:yyyy-MM-dd}");

        // Ausência de lançamento é resposta de negócio válida, não 404.
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        var consolidado = await resposta.Content.ReadFromJsonAsync<ConsolidadoDiarioResponse>();
        consolidado!.QuantidadeLancamentos.Should().Be(0);
        consolidado.SaldoLiquido.Should().Be(0m);
    }

    [Fact]
    public async Task Deve_recusar_acesso_sem_token()
    {
        var clienteAnonimo = _fixture.Factory.CreateClient();
        var data = DateOnly.FromDateTime(DateTime.Today);

        var consolidado = await clienteAnonimo.GetAsync($"/consolidado/diario?data={data:yyyy-MM-dd}");
        consolidado.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var lancamento = await clienteAnonimo.PostAsJsonAsync("/lancamentos", new
        {
            valor = 10m, tipo = "Credito", data = data.ToString("yyyy-MM-dd"), descricao = "x", categoria = "Vendas"
        });
        lancamento.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
