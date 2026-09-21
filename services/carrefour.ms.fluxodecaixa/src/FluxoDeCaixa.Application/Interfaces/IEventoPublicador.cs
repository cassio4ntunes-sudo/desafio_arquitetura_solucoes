using FluxoDeCaixa.Domain.Eventos;

namespace FluxoDeCaixa.Application.Interfaces;

public interface IEventoPublicador
{
    Task PublicarLancamentoRegistradoAsync(LancamentoRegistrado evento, CancellationToken ct);
}
