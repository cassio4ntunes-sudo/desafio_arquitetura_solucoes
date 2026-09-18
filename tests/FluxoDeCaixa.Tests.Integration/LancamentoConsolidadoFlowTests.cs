using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FluxoDeCaixa.Api.DTOs;
using FluxoDeCaixa.Tests.Integration.Fixtures;

namespace FluxoDeCaixa.Tests.Integration;

[Collection("Integration")]
public class LancamentoConsolidadoFlowTests
{
    private readonly IntegrationTestFixture _fixture;

    public LancamentoConsolidadoFlowTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Deve_consolidar_lancamento_credito_via_fluxo_completo()
    {
        // Arrange — comerciante próprio para não colidir com outros testes
        var client = await _fixture.CriarClienteAutenticadoAsync(IntegrationTestFixture.ComercianteA);
        var data = DateOnly.FromDateTime(DateTime.Today);
        var request = new { valor = 250.50m, tipo = "Credito", data = data.ToString("yyyy-MM-dd"), descricao = "Venda teste E2E", categoria = "Vendas" };

        // Act — POST /lancamentos
        var postResponse = await client.PostAsJsonAsync("/lancamentos", request);

        // Assert — 201 Created
        postResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Wait for consumer to process and consolidate
        var consolidado = await WaitForConsolidadoAsync(client, data, TimeSpan.FromSeconds(15));

        // Assert — consolidado matches
        consolidado.Should().NotBeNull();
        consolidado!.TotalCreditos.Should().Be(250.50m);
        consolidado.SaldoLiquido.Should().Be(250.50m);
        consolidado.QuantidadeLancamentos.Should().Be(1);
    }

    [Fact]
    public async Task Deve_consolidar_lancamento_debito_e_credito_somados()
    {
        // Arrange
        var client = await _fixture.CriarClienteAutenticadoAsync(IntegrationTestFixture.ComercianteA);
        var data = DateOnly.FromDateTime(DateTime.Today.AddDays(-10));

        var credito = new { valor = 300.00m, tipo = "Credito", data = data.ToString("yyyy-MM-dd"), descricao = "Crédito E2E", categoria = "Vendas" };
        var debito = new { valor = 100.00m, tipo = "Debito", data = data.ToString("yyyy-MM-dd"), descricao = "Débito E2E", categoria = "Serviços" };

        // Act — POST credit then debit
        var postCredito = await client.PostAsJsonAsync("/lancamentos", credito);
        var postDebito = await client.PostAsJsonAsync("/lancamentos", debito);

        // Assert — both 201
        postCredito.StatusCode.Should().Be(HttpStatusCode.Created);
        postDebito.StatusCode.Should().Be(HttpStatusCode.Created);

        // Wait for consumer to consolidate both
        var consolidado = await WaitForConsolidadoAsync(
            client, data, TimeSpan.FromSeconds(15), quantidadeEsperada: 2);

        // Assert — consolidado sums both
        consolidado.Should().NotBeNull();
        consolidado!.TotalCreditos.Should().Be(300.00m);
        consolidado.TotalDebitos.Should().Be(100.00m);
        consolidado.SaldoLiquido.Should().Be(200.00m);
        consolidado.QuantidadeLancamentos.Should().Be(2);
    }

    /// <summary>
    /// Isolamento multi-tenant: o consolidado é por comerciante. O lançamento de
    /// um comerciante não pode aparecer — nem somado — no saldo de outro.
    /// </summary>
    [Fact]
    public async Task Consolidado_de_um_comerciante_nao_vaza_para_outro()
    {
        // Arrange — dois comerciantes distintos, mesma data
        var clienteA = await _fixture.CriarClienteAutenticadoAsync(IntegrationTestFixture.ComercianteA);
        var clienteB = await _fixture.CriarClienteAutenticadoAsync(IntegrationTestFixture.ComercianteB);
        var data = DateOnly.FromDateTime(DateTime.Today.AddDays(-30));

        // Act — só o comerciante A lança
        var post = await clienteA.PostAsJsonAsync("/lancamentos", new
        {
            valor = 999.99m,
            tipo = "Credito",
            data = data.ToString("yyyy-MM-dd"),
            descricao = "Venda exclusiva do comerciante A",
            categoria = "Vendas"
        });
        post.StatusCode.Should().Be(HttpStatusCode.Created);

        var consolidadoA = await WaitForConsolidadoAsync(clienteA, data, TimeSpan.FromSeconds(15));

        // Assert — A enxerga o próprio lançamento
        consolidadoA.Should().NotBeNull();
        consolidadoA!.TotalCreditos.Should().Be(999.99m);

        // Assert — B enxerga dia zerado, nunca o valor de A
        var respostaB = await clienteB.GetAsync($"/consolidado/diario?data={data:yyyy-MM-dd}");
        respostaB.StatusCode.Should().Be(HttpStatusCode.OK);

        var consolidadoB = await respostaB.Content.ReadFromJsonAsync<ConsolidadoDiarioResponse>();
        consolidadoB.Should().NotBeNull();
        consolidadoB!.TotalCreditos.Should().Be(0m);
        consolidadoB.SaldoLiquido.Should().Be(0m);
        consolidadoB.QuantidadeLancamentos.Should().Be(0);
    }

    [Fact]
    public async Task Deve_recusar_acesso_sem_token()
    {
        // Arrange — cliente sem Authorization header
        var clienteAnonimo = _fixture.Factory.CreateClient();
        var data = DateOnly.FromDateTime(DateTime.Today);

        // Act + Assert — ambos os endpoints de negócio exigem autenticação
        var consolidado = await clienteAnonimo.GetAsync($"/consolidado/diario?data={data:yyyy-MM-dd}");
        consolidado.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var lancamento = await clienteAnonimo.PostAsJsonAsync("/lancamentos", new
        {
            valor = 10m, tipo = "Credito", data = data.ToString("yyyy-MM-dd"), descricao = "x", categoria = "Vendas"
        });
        lancamento.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Deve_retornar_201_com_correlation_id_no_header()
    {
        // Arrange — os comerciantes do realm são fixos e compartilhados entre os
        // testes, então cada teste usa uma data própria para não somar no
        // consolidado que outro teste verifica.
        var client = await _fixture.CriarClienteAutenticadoAsync(IntegrationTestFixture.ComercianteA);
        var data = DateOnly.FromDateTime(DateTime.Today.AddDays(-50));
        var request = new { valor = 50.00m, tipo = "Credito", data = data.ToString("yyyy-MM-dd"), descricao = "Teste correlation", categoria = "Vendas" };

        // Act
        var response = await client.PostAsJsonAsync("/lancamentos", request);

        // Assert — 201 + correlation header
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Should().ContainKey("X-Correlation-Id");
        response.Headers.GetValues("X-Correlation-Id").First().Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// O consolidado é atualizado de forma assíncrona (RabbitMQ), então a leitura
    /// faz polling até a quantidade esperada aparecer. Um dia sem movimento
    /// responde 200 com zeros, por isso esperamos pela quantidade e não pelo status.
    /// </summary>
    private static async Task<ConsolidadoDiarioResponse?> WaitForConsolidadoAsync(
        HttpClient client, DateOnly data, TimeSpan timeout, int quantidadeEsperada = 1)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var response = await client.GetAsync($"/consolidado/diario?data={data:yyyy-MM-dd}");

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var consolidado = await response.Content.ReadFromJsonAsync<ConsolidadoDiarioResponse>();

                if (consolidado is not null && consolidado.QuantidadeLancamentos >= quantidadeEsperada)
                    return consolidado;
            }

            await Task.Delay(250);
        }

        return null;
    }
}
