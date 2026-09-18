using FluxoDeCaixa.Consolidacao.Modelos;
using FluxoDeCaixa.Consolidacao.Repositorios;
using MediatR;

namespace FluxoDeCaixa.Consolidacao.Consultas;

public class ConsultarConsolidadoDiarioHandler
    : IRequestHandler<ConsultarConsolidadoDiario, ConsolidadoDiario>
{
    private readonly IConsolidadoRepository _repository;

    public ConsultarConsolidadoDiarioHandler(IConsolidadoRepository repository)
        => _repository = repository;

    /// <summary>
    /// Dia sem movimento devolve consolidado zerado (nao 404) — a ausencia de
    /// lancamentos e uma resposta valida de negocio, nao um recurso inexistente.
    /// </summary>
    public async Task<ConsolidadoDiario> Handle(
        ConsultarConsolidadoDiario consulta, CancellationToken ct)
    {
        var consolidado = await _repository.ObterPorDataAsync(
            consulta.ComercianteId, consulta.Data, ct);

        return consolidado ?? ConsolidadoDiario.Vazio(consulta.ComercianteId, consulta.Data);
    }
}
