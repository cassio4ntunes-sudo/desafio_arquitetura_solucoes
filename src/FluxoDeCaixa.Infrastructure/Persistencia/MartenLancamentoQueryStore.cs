using FluxoDeCaixa.Application.Comum;
using FluxoDeCaixa.Application.Interfaces;
using FluxoDeCaixa.Domain.Agregados;
using FluxoDeCaixa.Domain.ModelosDeLeitura;
using Marten;

namespace FluxoDeCaixa.Infrastructure.Persistencia;

public class MartenLancamentoQueryStore : ILancamentoQueryStore
{
    private readonly IQuerySession _session;

    public MartenLancamentoQueryStore(IQuerySession session)
        => _session = session;

    public async Task<ResultadoPaginado<LancamentoResumo>> ConsultarPorDataAsync(
        Guid comercianteId, DateOnly data, int pagina, int tamanhoPagina, CancellationToken ct)
    {
        var query = _session.Query<Lancamento>()
            .Where(l => l.ComercianteId == comercianteId && l.Data == data);

        var total = await query.CountAsync(ct);

        var itens = await query
            .OrderByDescending(l => l.RegistradoEm)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(ct);

        var resumos = itens.Select(l => new LancamentoResumo(
            l.Id, l.ComercianteId, l.Valor, l.Tipo, l.Moeda, l.Descricao, l.Categoria, l.Data, l.RegistradoEm
        )).ToList();

        return new ResultadoPaginado<LancamentoResumo>(
            Itens: resumos,
            Pagina: pagina,
            TamanhoPagina: tamanhoPagina,
            Total: total
        );
    }
}
