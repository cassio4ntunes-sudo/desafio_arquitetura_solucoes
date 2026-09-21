namespace FluxoDeCaixa.Domain.Eventos;

public record LancamentoRegistrado(
    Guid LancamentoId,
    Guid ComercianteId,
    decimal Valor,
    string Tipo,              // "Debito" | "Credito"
    string Moeda,             // "BRL"
    string? Descricao,
    string Categoria,
    DateOnly Data,            // D-09: DateOnly for balance date
    DateTimeOffset RegistradoEm  // D-08: UTC timestamp
);
