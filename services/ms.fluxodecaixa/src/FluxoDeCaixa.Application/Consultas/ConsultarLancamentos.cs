using FluxoDeCaixa.Application.Comum;
using FluxoDeCaixa.Domain.ModelosDeLeitura;
using MediatR;

namespace FluxoDeCaixa.Application.Consultas;

public record ConsultarLancamentos(
    Guid ComercianteId,
    DateOnly Data,
    int Pagina = 1,
    int TamanhoPagina = 20
) : IRequest<ResultadoPaginado<LancamentoResumo>>;
