using FluxoDeCaixa.Application.Interfaces;
using FluxoDeCaixa.Domain.Eventos;
using MassTransit;

namespace FluxoDeCaixa.Infrastructure.Mensageria;

public class MassTransitEventoPublicador : IEventoPublicador
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitEventoPublicador(IPublishEndpoint publishEndpoint)
        => _publishEndpoint = publishEndpoint;

    public async Task PublicarLancamentoRegistradoAsync(
        LancamentoRegistrado evento, CancellationToken ct)
    {
        await _publishEndpoint.Publish(evento, ct);
    }
}
