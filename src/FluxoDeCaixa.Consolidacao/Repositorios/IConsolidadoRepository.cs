using FluxoDeCaixa.Consolidacao.Modelos;
using FluxoDeCaixa.Domain.Eventos;

namespace FluxoDeCaixa.Consolidacao.Repositorios;

public interface IConsolidadoRepository
{
    Task UpsertAsync(LancamentoRegistrado evento, CancellationToken ct);
    Task<ConsolidadoDiario?> ObterPorDataAsync(Guid comercianteId, DateOnly data, CancellationToken ct);
    Task CriarTabelaSeNaoExisteAsync(CancellationToken ct);
}
