namespace FluxoDeCaixa.Api.DTOs;

public record RegistrarLancamentoRequest(
    decimal Valor,
    string Tipo,        // "Debito" | "Credito"
    DateOnly Data,
    string? Descricao,
    string Categoria
);
