namespace FluxoDeCaixa.Domain.ModelosDeLeitura;

public record LancamentoResumo(
    Guid Id,
    Guid ComercianteId,
    decimal Valor,
    string Tipo,
    string Moeda,
    string? Descricao,
    string Categoria,
    DateOnly Data,
    DateTimeOffset RegistradoEm
);
