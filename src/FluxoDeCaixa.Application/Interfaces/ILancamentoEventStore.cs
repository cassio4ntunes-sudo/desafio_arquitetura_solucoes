using FluxoDeCaixa.Domain.Eventos;

namespace FluxoDeCaixa.Application.Interfaces;

public interface ILancamentoEventStore
{
    Task<Guid> RegistrarAsync(LancamentoRegistrado evento, CancellationToken ct);
}
