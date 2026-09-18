using FluxoDeCaixa.Consolidacao.Consultas;
using FluxoDeCaixa.Consolidacao.Repositorios;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FluxoDeCaixa.Consolidacao;

public static class ConsolidacaoServiceExtensions
{
    public static IServiceCollection AddConsolidacaoServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Repository — same PostgreSQL as Marten, different table (per D-10)
        // Connection resolved lazily from IConfiguration — enables WebApplicationFactory config override
        services.AddScoped<IConsolidadoRepository>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            return new PostgresConsolidadoRepository(config.GetConnectionString("Marten")!);
        });

        // MediatR handlers from Consolidacao assembly
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<ConsultarConsolidadoDiarioHandler>());

        return services;
    }

    public static async Task EnsureConsolidadoTableAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IConsolidadoRepository>();
        await repository.CriarTabelaSeNaoExisteAsync(CancellationToken.None);
    }
}
