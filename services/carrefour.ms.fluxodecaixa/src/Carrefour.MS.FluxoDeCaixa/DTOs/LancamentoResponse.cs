namespace FluxoDeCaixa.Api.DTOs;

public record LancamentoResponse(
    Guid Id,
    decimal Valor,
    string Tipo,
    string Moeda,
    string? Descricao,
    string Categoria,
    DateOnly Data,
    DateTimeOffset RegistradoEm
);
