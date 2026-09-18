using System.Net.Http.Headers;
using System.Net.Http.Json;
using NBomber.CSharp;
using NBomber.Http.CSharp;

namespace FluxoDeCaixa.Tests.Load;

/// <summary>
/// RNF do desafio: "em dias de picos, o serviço de consolidado diário recebe
/// 50 requisições por segundo, com no máximo 5% de perda de requisições".
///
/// O teste exercita o caminho real e completo: autentica no Keycloak, lança
/// créditos e débitos pela API (que passam por Marten + RabbitMQ + consumer)
/// e só então aplica a carga de 50 req/s por 2 minutos sobre
/// GET /consolidado/diario com o Bearer token — sem semear a tabela por SQL
/// e sem bater em endpoint anônimo.
///
/// Requer o ambiente no ar: docker compose up -d --wait
/// Rodar com: dotnet test tests/FluxoDeCaixa.Tests.Load
/// </summary>
public class ConsolidadoLoadTests
{
    private const string BaseUrl = "http://localhost:5000";
    private const string KeycloakTokenUrl =
        "http://localhost:8081/realms/fluxocaixa/protocol/openid-connect/token";
    private const int TaxaRequisicoesPorSegundo = 50;
    private static readonly TimeSpan Duracao = TimeSpan.FromMinutes(2);
    private const double PercentualDeErroMaximo = 5.0;

    /// <summary>
    /// Obtém um access token real do Keycloak via direct grant. O cliente
    /// `fluxocaixa-testes` existe só para automação — o frontend usa PKCE.
    /// </summary>
    private static async Task<string> ObterTokenAsync()
    {
        using var client = new HttpClient();

        var resposta = await client.PostAsync(KeycloakTokenUrl,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = "fluxocaixa-testes",
                ["grant_type"] = "password",
                ["username"] = "demo@loja.com",
                ["password"] = "Senha123!",
                ["scope"] = "openid profile email"
            }));

        var corpo = await resposta.Content.ReadAsStringAsync();

        if (!resposta.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Keycloak não emitiu token: {(int)resposta.StatusCode} {corpo}. " +
                "O ambiente está no ar? (docker compose up -d --wait)");

        using var json = System.Text.Json.JsonDocument.Parse(corpo);
        return json.RootElement.GetProperty("access_token").GetString()!;
    }

    /// <summary>
    /// Autentica no Keycloak e registra lançamentos reais pela API, aguardando a
    /// consolidação assíncrona. Devolve o token e a data com movimento.
    /// </summary>
    private static async Task<(string Token, DateOnly Data)> PrepararCenarioAsync()
    {
        using var client = new HttpClient { BaseAddress = new Uri(BaseUrl) };

        // Falha cedo e com mensagem clara se o ambiente não estiver no ar
        try
        {
            var saude = await client.GetAsync("/health/ready");
            if (!saude.IsSuccessStatusCode)
                throw new InvalidOperationException($"/health/ready respondeu {(int)saude.StatusCode}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException(
                $"A API não respondeu em {BaseUrl}. Suba o ambiente antes do teste de carga: " +
                "docker compose up -d --wait", ex);
        }

        var token = await ObterTokenAsync();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var data = DateOnly.FromDateTime(DateTime.Today);

        // 20 lançamentos pelo caminho real (API → Marten → RabbitMQ → consumer)
        for (var i = 1; i <= 20; i++)
        {
            var resposta = await client.PostAsJsonAsync("/lancamentos", new
            {
                valor = 100.00m + i,
                tipo = i % 4 == 0 ? "Debito" : "Credito",
                data = data.ToString("yyyy-MM-dd"),
                descricao = $"Carga {i}",
                categoria = "Vendas"
            });
            resposta.EnsureSuccessStatusCode();
        }

        // Aguarda a consolidação assíncrona refletir os lançamentos
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var resposta = await client.GetAsync($"/consolidado/diario?data={data:yyyy-MM-dd}");
            if (resposta.IsSuccessStatusCode)
            {
                var corpo = await resposta.Content.ReadFromJsonAsync<ConsolidadoResposta>();
                if (corpo is not null && corpo.QuantidadeLancamentos >= 20)
                    return (token, data);
            }
            await Task.Delay(500);
        }

        throw new InvalidOperationException(
            "A consolidação não processou os 20 lançamentos em 30s. O ambiente está no ar? (docker compose up -d --wait)");
    }

    private record ConsolidadoResposta(
        DateOnly Data, decimal TotalCreditos, decimal TotalDebitos,
        decimal SaldoLiquido, int QuantidadeLancamentos);

    [Fact]
    public async Task Deve_suportar_50_requisicoes_por_segundo_por_2_minutos()
    {
        // Arrange — comerciante autenticado com movimento real no dia
        var (token, data) = await PrepararCenarioAsync();

        using var httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var scenario = Scenario.Create("get_consolidado_diario", async context =>
        {
            var request = Http
                .CreateRequest("GET", $"/consolidado/diario?data={data:yyyy-MM-dd}")
                .WithHeader("Authorization", $"Bearer {token}");

            return await Http.Send(httpClient, request);
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.Inject(
                rate: TaxaRequisicoesPorSegundo,
                interval: TimeSpan.FromSeconds(1),
                during: Duracao)
        );

        // Act
        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .WithReportFolder("reports/carga-consolidado")
            .Run();

        // Assert — taxa de erro ≤ 5% (RNF do desafio)
        var scenarioStats = stats.ScenarioStats[0];
        var totalRequisicoes = scenarioStats.AllRequestCount;
        var falhas = scenarioStats.Fail.Request.Count;
        var taxaDeErro = totalRequisicoes == 0 ? 100.0 : falhas * 100.0 / totalRequisicoes;

        Assert.True(
            totalRequisicoes >= TaxaRequisicoesPorSegundo * Duracao.TotalSeconds * 0.9,
            $"Volume abaixo do esperado: {totalRequisicoes} requisições.");

        Assert.True(
            taxaDeErro <= PercentualDeErroMaximo,
            $"Taxa de erro {taxaDeErro:F2}% acima do limite de {PercentualDeErroMaximo}%. " +
            $"Total: {totalRequisicoes}, Falhas: {falhas}");
    }
}
