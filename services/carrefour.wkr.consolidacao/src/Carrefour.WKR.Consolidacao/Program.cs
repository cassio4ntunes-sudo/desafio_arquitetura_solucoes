using Carrefour.WKR.Consolidacao.Saude;
using FluxoDeCaixa.Consolidacao;
using FluxoDeCaixa.Consolidacao.Consumidores;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Formatting.Json;

// ─────────────────────────────────────────────────────────────────────────────
// Carrefour.WKR.Consolidacao
//
// Worker do domínio de Consolidação (ADR-15). Consome o evento
// LancamentoRegistrado do RabbitMQ e materializa o read model consolidado_diario.
//
// NÃO expõe endpoint de negócio: a única superfície HTTP são as probes de saúde,
// necessárias para o orquestrador. Quem atende o comerciante é o
// Carrefour.MS.FluxoDeCaixa.
//
// Este serviço pode cair, ser reimplantado ou escalar sozinho sem afetar o
// registro de lançamentos — é a garantia que o RNF do desafio exige, agora
// também no nível de processo e não só de protocolo.
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("servico", "Carrefour.WKR.Consolidacao")
    .WriteTo.Console(new JsonFormatter()));

// Repositório do read model (escrita idempotente — ADR-10)
builder.Services.AddConsolidadoRepositorio(builder.Configuration);

// MassTransit + RabbitMQ com o consumer da consolidação.
// A configuração do broker vive aqui, e não numa biblioteca compartilhada,
// porque cada serviço é dono da própria conexão com a mensageria.
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<LancamentoRegistradoConsumer, LancamentoRegistradoConsumerDefinition>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var config = context.GetRequiredService<IConfiguration>();
        var host = config["RabbitMq:Host"] ?? "localhost";
        var port = ushort.TryParse(config["RabbitMq:Port"], out var p) ? p : (ushort)5672;

        cfg.Host(host, port, "/", h =>
        {
            h.Username(config["RabbitMq:Username"] ?? "guest");
            h.Password(config["RabbitMq:Password"] ?? "guest");
        });

        cfg.ConfigureEndpoints(context);
    });
});

// Health checks — liveness (processo vivo) e readiness (PostgreSQL alcançável).
// O RabbitMQ não entra no readiness de propósito: o MassTransit reconecta
// sozinho, e marcar o worker como "não pronto" por uma oscilação do broker só
// causaria reinícios inúteis. A fila retém as mensagens enquanto isso.
var connStr = builder.Configuration.GetConnectionString("Marten")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Marten não configurado — o worker precisa do PostgreSQL do read model.");

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddCheck("postgres", new PostgresHealthCheck(connStr), tags: new[] { "ready" });

var app = builder.Build();

// DDL idempotente do read model e do inbox (ver comentário em ConsolidacaoServiceExtensions)
await app.Services.EnsureConsolidadoTableAsync();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.Run();

namespace Carrefour.WKR.Consolidacao
{
    /// <summary>
    /// Âncora pública para os testes de integração hospedarem este worker via
    /// <c>WebApplicationFactory&lt;PontoDeEntrada&gt;</c>.
    ///
    /// Não declaramos <c>public partial class Program</c> aqui de propósito: o
    /// Carrefour.MS.FluxoDeCaixa já expõe um <c>Program</c> público no namespace
    /// global, e dois tipos homônimos visíveis no mesmo projeto de teste dariam
    /// ambiguidade de compilação. Como a WebApplicationFactory localiza o ponto
    /// de entrada pelo <i>assembly</i> do tipo informado, qualquer tipo público
    /// deste projeto serve de âncora.
    /// </summary>
    public sealed class PontoDeEntrada;
}
