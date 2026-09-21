using FluxoDeCaixa.Consolidacao.Modelos;
using MediatR;

namespace FluxoDeCaixa.Consolidacao.Consultas;

public record ConsultarConsolidadoDiario(Guid ComercianteId, DateOnly Data)
    : IRequest<ConsolidadoDiario>;
