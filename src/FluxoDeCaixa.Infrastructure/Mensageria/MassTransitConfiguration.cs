using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FluxoDeCaixa.Infrastructure.Mensageria;

public static class MassTransitConfiguration
{
    public static IServiceCollection AddMassTransitBus(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        services.AddMassTransit(x =>
        {
            configureConsumers?.Invoke(x);

            x.UsingRabbitMq((context, cfg) =>
            {
                // Resolve IConfiguration lazily so WebApplicationFactory overrides are visible
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

        return services;
    }
}
