using FluxoDeCaixa.Application.Comum;
using FluxoDeCaixa.Application.Interfaces;
using FluxoDeCaixa.Domain.ModelosDeLeitura;
using MediatR;

namespace FluxoDeCaixa.Application.Consultas;

public class ConsultarLancamentosHandler
    : IRequestHandler<ConsultarLancamentos, ResultadoPaginado<LancamentoResumo>>
{
    private readonly ILancamentoQueryStore _queryStore;

    public ConsultarLancamentosHandler(ILancamentoQueryStore queryStore)
        => _queryStore = queryStore;

    public async Task<ResultadoPaginado<LancamentoResumo>> Handle(
        ConsultarLancamentos consulta, CancellationToken ct)
    {
        return await _queryStore.ConsultarPorDataAsync(
            consulta.ComercianteId, consulta.Data, consulta.Pagina, consulta.TamanhoPagina, ct);
    }
}
