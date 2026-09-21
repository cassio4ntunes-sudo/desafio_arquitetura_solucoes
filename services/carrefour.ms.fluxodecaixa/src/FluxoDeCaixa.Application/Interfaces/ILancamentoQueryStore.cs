using FluxoDeCaixa.Application.Comum;
using FluxoDeCaixa.Domain.ModelosDeLeitura;

namespace FluxoDeCaixa.Application.Interfaces;

public interface ILancamentoQueryStore
{
    Task<ResultadoPaginado<LancamentoResumo>> ConsultarPorDataAsync(
        Guid comercianteId, DateOnly data, int pagina, int tamanhoPagina, CancellationToken ct);
}
