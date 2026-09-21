namespace FluxoDeCaixa.Domain.ObjetosDeValor;

public readonly record struct Dinheiro
{
    public decimal Valor { get; }
    public string Moeda { get; }

    public Dinheiro(decimal valor, string moeda = "BRL")
    {
        if (valor < 0)
            throw new ArgumentException("Valor não pode ser negativo", nameof(valor));
        if (decimal.Round(valor, 2) != valor)
            throw new ArgumentException("Máximo 2 casas decimais", nameof(valor));

        Valor = valor;
        Moeda = moeda;
    }

    public static Dinheiro operator +(Dinheiro a, Dinheiro b)
    {
        if (a.Moeda != b.Moeda)
            throw new InvalidOperationException($"Moedas diferentes: {a.Moeda} vs {b.Moeda}");
        return new Dinheiro(a.Valor + b.Valor, a.Moeda);
    }

    public static Dinheiro Zero(string moeda = "BRL") => new(0m, moeda);
}
