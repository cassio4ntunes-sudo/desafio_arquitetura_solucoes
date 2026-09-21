using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FluxoDeCaixa.Tests.Integration.Fixtures;

namespace FluxoDeCaixa.Tests.Integration;

/// <summary>
/// Caminho de ESCRITA do serviço de lançamentos.
///
/// Todos os testes abaixo rodam com a consolidação <b>completamente ausente</b>
/// — o worker não existe neste repositório. É a forma mais forte de demonstrar
/// o requisito não-funcional do enunciado: o registro de lançamentos não
/// depende, em nenhum ponto, do serviço de consolidado diário.
/// </summary>
[Collection("Integration")]
public class EscritaDeLancamentosTests
{
    private readonly IntegrationTestFixture _fixture;

    public EscritaDeLancamentosTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Deve_registrar_lancamento_e_devolver_o_id()
    {
        var client = await _fixture.CriarClienteAutenticadoAsync(IntegrationTestFixture.ComercianteA);
        var data = DateOnly.FromDateTime(DateTime.Today);

        var resposta = await client.PostAsJsonAsync("/lancamentos", new
        {
            valor = 250.50m,
            tipo = "Credito",
            data = data.ToString("yyyy-MM-dd"),
            descricao = "Venda de produtos",
            categoria = "Vendas"
        });

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);

        var corpo = await resposta.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        corpo.Should().ContainKey("id");
        corpo!["id"].ToString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Deve_consultar_os_proprios_lancamentos_do_dia()
    {
        var client = await _fixture.CriarClienteAutenticadoAsync(IntegrationTestFixture.ComercianteA);
        var data = DateOnly.FromDateTime(DateTime.Today.AddDays(-80));

        await client.PostAsJsonAsync("/lancamentos", new
        {
            valor = 75.25m, tipo = "Debito", data = data.ToString("yyyy-MM-dd"),
            descricao = "Pagamento fornecedor", categoria = "Serviços"
        });

        var resposta = await client.GetAsync($"/lancamentos?data={data:yyyy-MM-dd}&pagina=1&tamanhoPagina=20");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("Pagamento fornecedor");
    }

    [Fact]
    public async Task Deve_rejeitar_lancamento_invalido_com_os_campos_em_erro()
    {
        var client = await _fixture.CriarClienteAutenticadoAsync(IntegrationTestFixture.ComercianteA);

        var resposta = await client.PostAsJsonAsync("/lancamentos", new
        {
            valor = 0m,
            tipo = "Credito",
            data = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd"),
            descricao = "Valor invalido",
            categoria = "Vendas"
        });

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("maior que zero");
    }

    [Fact]
    public async Task Deve_retornar_correlation_id_no_header()
    {
        var client = await _fixture.CriarClienteAutenticadoAsync(IntegrationTestFixture.ComercianteA);
        var data = DateOnly.FromDateTime(DateTime.Today.AddDays(-81));

        var resposta = await client.PostAsJsonAsync("/lancamentos", new
        {
            valor = 50.00m, tipo = "Credito", data = data.ToString("yyyy-MM-dd"),
            descricao = "Teste correlation", categoria = "Vendas"
        });

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        resposta.Headers.Should().ContainKey("X-Correlation-Id");
        resposta.Headers.GetValues("X-Correlation-Id").First().Should().NotBeNullOrEmpty();
    }
}
