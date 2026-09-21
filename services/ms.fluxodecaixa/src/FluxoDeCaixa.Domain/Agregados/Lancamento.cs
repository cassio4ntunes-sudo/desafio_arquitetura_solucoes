using FluxoDeCaixa.Domain.Eventos;

namespace FluxoDeCaixa.Domain.Agregados;

public record Lancamento
{
    public Guid Id { get; init; }
    public Guid ComercianteId { get; init; }
    public decimal Valor { get; init; }
    public string Tipo { get; init; } = default!;
    public string Moeda { get; init; } = "BRL";
    public string? Descricao { get; init; }
    public string Categoria { get; init; } = default!;
    public DateOnly Data { get; init; }
    public DateTimeOffset RegistradoEm { get; init; }

    // Marten convention: static Create for the first event in a stream
    public static Lancamento Create(LancamentoRegistrado e) => new()
    {
        Id = e.LancamentoId,
        ComercianteId = e.ComercianteId,
        Valor = e.Valor,
        Tipo = e.Tipo,
        Moeda = e.Moeda,
        Descricao = e.Descricao,
        Categoria = e.Categoria,
        Data = e.Data,
        RegistradoEm = e.RegistradoEm
    };
}
