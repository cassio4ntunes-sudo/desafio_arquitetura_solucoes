using FluxoDeCaixa.Application.Interfaces;
using FluxoDeCaixa.Domain.Agregados;
using FluxoDeCaixa.Domain.Eventos;
using Marten;

namespace FluxoDeCaixa.Infrastructure.Persistencia;

public class MartenLancamentoEventStore : ILancamentoEventStore
{
    private readonly IDocumentSession _session;

    public MartenLancamentoEventStore(IDocumentSession session)
        => _session = session;

    public async Task<Guid> RegistrarAsync(LancamentoRegistrado evento, CancellationToken ct)
    {
        var id = evento.LancamentoId;
        _session.Events.StartStream<Lancamento>(id, evento);
        await _session.SaveChangesAsync(ct);
        return id;
    }
}
