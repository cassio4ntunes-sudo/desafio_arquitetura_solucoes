using MassTransit;

namespace FluxoDeCaixa.Consolidacao.Consumidores;

public class LancamentoRegistradoConsumerDefinition
    : ConsumerDefinition<LancamentoRegistradoConsumer>
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<LancamentoRegistradoConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r => r.Intervals(
            TimeSpan.FromMilliseconds(500),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(10)));
    }
}
