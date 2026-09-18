namespace FluxoDeCaixa.Api.DTOs;

public record ConsolidadoDiarioResponse(
    DateOnly Data,
    decimal TotalCreditos,
    decimal TotalDebitos,
    decimal SaldoLiquido,
    int QuantidadeLancamentos
);
