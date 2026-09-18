using MediatR;

namespace FluxoDeCaixa.Application.Comandos;

public record RegistrarLancamento(
    Guid ComercianteId,
    decimal Valor,
    string Tipo,        // "Debito" | "Credito"
    DateOnly Data,      // D-09: DateOnly
    string? Descricao,
    string Categoria
) : IRequest<Guid>;
