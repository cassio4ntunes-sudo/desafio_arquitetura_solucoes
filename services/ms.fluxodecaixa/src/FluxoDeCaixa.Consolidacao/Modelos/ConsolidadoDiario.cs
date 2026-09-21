namespace FluxoDeCaixa.Consolidacao.Modelos;

public record ConsolidadoDiario(
    Guid ComercianteId,
    DateOnly Data,
    decimal TotalCreditos,
    decimal TotalDebitos,
    decimal SaldoLiquido,
    int Quantidade,
    DateTimeOffset AtualizadoEm
)
{
    /// <summary>Consolidado zerado — dia sem movimento para o comerciante.</summary>
    public static ConsolidadoDiario Vazio(Guid comercianteId, DateOnly data)
        => new(comercianteId, data, 0m, 0m, 0m, 0, DateTimeOffset.UtcNow);
}
