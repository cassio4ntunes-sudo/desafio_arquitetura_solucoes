using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

using Testcontainers.Keycloak;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace FluxoDeCaixa.Tests.Integration.Fixtures;

/// <summary>
/// Sobe PostgreSQL, RabbitMQ e <b>Keycloak</b> reais (Testcontainers) e hospeda
/// em memória <b>apenas o MS.FluxoDeCaixa</b>.
///
/// O worker de consolidação NÃO existe neste repositório — é outro serviço, com
/// ciclo de vida próprio (ADR-15). Tudo o que passa aqui, passa com a
/// consolidação ausente.
///
/// O Keycloak importa o mesmo realm do ambiente local, então a cadeia de
/// autenticação é exercitada de verdade: token RS256 emitido pelo IdP e
/// validado pela API contra o JWKS do realm.
/// </summary>
public class IntegrationTestFixture : IAsyncLifetime
{
    private const string Realm = "fluxocaixa";
    private const string ClientDeTestes = "fluxocaixa-testes";
    private const string SenhaPadrao = "Senha123!";

    /// <summary>Comerciantes pré-cadastrados no realm, com id fixo.</summary>
    public const string ComercianteDemo = "demo@loja.com";
    public const string ComercianteA = "comerciante-a@teste.com";
    public const string ComercianteB = "comerciante-b@teste.com";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    private readonly RabbitMqContainer _rabbitmq = new RabbitMqBuilder("rabbitmq:3-management")
        .Build();

    private readonly KeycloakContainer _keycloak = new KeycloakBuilder()
        .WithImage("quay.io/keycloak/keycloak:26.0")
        .WithResourceMapping(
            new FileInfo(CaminhoDoRealm()),
            new FileInfo("/opt/keycloak/data/import/realm-fluxocaixa.json"))
        .WithCommand("--import-realm")
        .Build();

    private WebApplicationFactory<FluxoDeCaixa.Api.PontoDeEntrada> _factory = default!;
    private HttpClient _tokenClient = default!;

    /// <summary>Cliente autenticado como o comerciante demo.</summary>
    public HttpClient Client { get; private set; } = default!;

    public WebApplicationFactory<FluxoDeCaixa.Api.PontoDeEntrada> Factory => _factory;

    /// <summary>Connection string do PostgreSQL de teste (Testcontainers).</summary>
    public string ConnectionString => _postgres.GetConnectionString();

    /// <summary>Issuer do realm, como a API o enxerga.</summary>
    public string Authority => $"{_keycloak.GetBaseAddress().TrimEnd('/')}/realms/{Realm}";

    /// <summary>
    /// O realm do Keycloak é <b>infraestrutura compartilhada</b>, não código deste
    /// serviço: vive na raiz do ambiente, junto do compose. Assim os testes
    /// exercitam exatamente a configuração de identidade que sobe em produção.
    ///
    /// Em multi-repo real este arquivo estaria no repositório de plataforma, e
    /// chegaria aqui por submodule, pacote ou download no pipeline.
    /// </summary>
    private static string CaminhoDoRealm()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null &&
               !File.Exists(Path.Combine(dir.FullName, "keycloak", "realm-fluxocaixa.json")))
            dir = dir.Parent;

        if (dir is null)
            throw new InvalidOperationException(
                "keycloak/realm-fluxocaixa.json não encontrado subindo a partir de " + AppContext.BaseDirectory);

        return Path.Combine(dir.FullName, "keycloak", "realm-fluxocaixa.json");
    }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _postgres.StartAsync(),
            _rabbitmq.StartAsync(),
            _keycloak.StartAsync());

        var infra = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Marten"] = _postgres.GetConnectionString(),
            ["RabbitMq:Host"] = _rabbitmq.Hostname,
            ["RabbitMq:Port"] = _rabbitmq.GetMappedPublicPort(5672).ToString(),
            ["RabbitMq:Username"] = "rabbitmq",
            ["RabbitMq:Password"] = "rabbitmq"
        };

        // ── Serviço de lançamentos (API) ──────────────────────────────────────
        _factory = new WebApplicationFactory<FluxoDeCaixa.Api.PontoDeEntrada>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    var cfg = new Dictionary<string, string?>(infra)
                    {
                        // Aqui issuer e metadata coincidem: o Keycloak é alcançado
                        // pela mesma URL pelo teste e pela API (mesmo host).
                        ["Keycloak:Authority"] = Authority,
                        ["Keycloak:Audience"] = "fluxocaixa-api",
                        ["Keycloak:RequireHttpsMetadata"] = "false"
                    };
                    config.AddInMemoryCollection(cfg);
                });
            });

        // NÃO há worker aqui — e isso é a essência do teste.
        //
        // Este "repositório" contém apenas o serviço de lançamentos. Nenhuma
        // linha do WKR.Consolidacao é compilada ou executada. Se algum
        // teste abaixo passar, ele passou com a consolidação 100% ausente — que
        // é exatamente o requisito não-funcional do enunciado.
        //
        // A validação de que o lançamento *chega* ao consolidado é ponta a ponta
        // entre serviços, e por isso vive em e2e/ contra o ambiente real.

        _tokenClient = new HttpClient { BaseAddress = new Uri(_keycloak.GetBaseAddress()) };

        Client = await CriarClienteAutenticadoAsync(ComercianteDemo);
    }

    /// <summary>
    /// Obtém um access token real do Keycloak via direct grant e devolve um
    /// HttpClient já com o Bearer. O direct grant existe só no cliente de testes
    /// (<c>fluxocaixa-testes</c>) — o cliente do browser usa apenas PKCE.
    /// </summary>
    public async Task<HttpClient> CriarClienteAutenticadoAsync(string usuario)
    {
        var token = await ObterTokenAsync(usuario);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task<string> ObterTokenAsync(string usuario)
    {
        var resposta = await _tokenClient.PostAsync(
            $"/realms/{Realm}/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = ClientDeTestes,
                ["grant_type"] = "password",
                ["username"] = usuario,
                ["password"] = SenhaPadrao,
                ["scope"] = "openid profile email"
            }));

        var corpo = await resposta.Content.ReadAsStringAsync();

        if (!resposta.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Keycloak não emitiu token para {usuario}: {(int)resposta.StatusCode} {corpo}");

        using var json = System.Text.Json.JsonDocument.Parse(corpo);
        return json.RootElement.GetProperty("access_token").GetString()
               ?? throw new InvalidOperationException("Resposta do Keycloak sem access_token.");
    }

    /// <summary>Id do comerciante (claim `sub`), para conferir o escopo dos dados.</summary>
    public static Guid IdDoComerciante(string usuario) => usuario switch
    {
        ComercianteDemo => Guid.Parse("11111111-1111-1111-1111-111111111111"),
        ComercianteA => Guid.Parse("22222222-2222-2222-2222-222222222222"),
        ComercianteB => Guid.Parse("33333333-3333-3333-3333-333333333333"),
        _ => throw new ArgumentOutOfRangeException(nameof(usuario), usuario, "Usuário não existe no realm de testes.")
    };

    public async Task DisposeAsync()
    {
        Client.Dispose();
        _tokenClient.Dispose();
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
        await _rabbitmq.DisposeAsync();
        await _keycloak.DisposeAsync();
    }
}

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<IntegrationTestFixture> { }
