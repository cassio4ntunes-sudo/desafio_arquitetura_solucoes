using Carrefour.WKR.Consolidacao;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Carrefour.WKR.Consolidacao.Tests.Fixtures;

/// <summary>
/// Sobe PostgreSQL e RabbitMQ reais (Testcontainers) e hospeda em memória
/// <b>apenas o Carrefour.WKR.Consolidacao</b>.
///
/// Não há Keycloak nem serviço de lançamentos aqui: este worker não tem
/// superfície autenticada e não conhece quem publica o evento. Ele reage a
/// uma mensagem no contrato acordado — e é exatamente isso que testamos.
/// </summary>
public class WorkerTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    private readonly RabbitMqContainer _rabbitmq = new RabbitMqBuilder("rabbitmq:3-management")
        .Build();

    private WebApplicationFactory<PontoDeEntrada> _factory = default!;
    private HttpClient _client = default!;

    /// <summary>Connection string do PostgreSQL de teste.</summary>
    public string ConnectionString => _postgres.GetConnectionString();

    /// <summary>Container de serviços do worker — dá acesso ao IBus para publicar.</summary>
    public IServiceProvider Services => _factory.Services;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitmq.StartAsync());

        _factory = new WebApplicationFactory<PontoDeEntrada>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Marten"] = _postgres.GetConnectionString(),
                        ["RabbitMq:Host"] = _rabbitmq.Hostname,
                        ["RabbitMq:Port"] = _rabbitmq.GetMappedPublicPort(5672).ToString(),
                        ["RabbitMq:Username"] = "rabbitmq",
                        ["RabbitMq:Password"] = "rabbitmq"
                    }));
            });

        // Força a inicialização do host: sem isto o hosted service do MassTransit
        // não sobe e nenhuma mensagem seria consumida.
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
        await _rabbitmq.DisposeAsync();
    }
}

[CollectionDefinition("Worker")]
public class WorkerCollection : ICollectionFixture<WorkerTestFixture> { }
