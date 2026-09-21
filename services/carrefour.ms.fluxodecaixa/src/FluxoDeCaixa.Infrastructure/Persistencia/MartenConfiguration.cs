using FluxoDeCaixa.Domain.Agregados;
using JasperFx.Events.Daemon;
using Marten;
using Marten.Events.Projections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FluxoDeCaixa.Infrastructure.Persistencia;

public static class MartenConfiguration
{
    public static IServiceCollection AddMartenEventStore(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMarten(opts =>
        {
            // CRITICAL: prevents async daemon event skipping under load (Pitfall 1)
            opts.Events.AppendMode = JasperFx.Events.EventAppendMode.Quick;

            // Inline snapshot: Lancamento is queryable immediately after write
            opts.Projections.Snapshot<Lancamento>(SnapshotLifecycle.Inline);

            // Event metadata for correlation ID propagation
            opts.Events.MetadataConfig.CorrelationIdEnabled = true;
            opts.Events.MetadataConfig.CausationIdEnabled = true;
        })
        .UseLightweightSessions()
        .AddAsyncDaemon(DaemonMode.Solo);  // Ready for Phase 2 projections

        // Connection resolved lazily — enables WebApplicationFactory config override in tests
        services.ConfigureMarten((sp, opts) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            opts.Connection(config.GetConnectionString("Marten")!);
        });

        return services;
    }
}
