using FluxoDeCaixa.Application.Interfaces;
using FluxoDeCaixa.Infrastructure.Mensageria;
using FluxoDeCaixa.Infrastructure.Persistencia;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FluxoDeCaixa.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        // Marten (event store + inline projections)
        services.AddMartenEventStore(configuration);

        // MassTransit + RabbitMQ (consumer registration via callback)
        services.AddMassTransitBus(configuration, configureConsumers);

        // Application interface implementations
        services.AddScoped<ILancamentoEventStore, MartenLancamentoEventStore>();
        services.AddScoped<ILancamentoQueryStore, MartenLancamentoQueryStore>();
        services.AddScoped<IEventoPublicador, MassTransitEventoPublicador>();

        return services;
    }
}
