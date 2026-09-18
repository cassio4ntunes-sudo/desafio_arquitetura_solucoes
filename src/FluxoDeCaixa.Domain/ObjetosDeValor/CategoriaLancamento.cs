namespace FluxoDeCaixa.Domain.ObjetosDeValor;

public static class CategoriaLancamento
{
    public const string Alimentacao = "Alimentação";
    public const string Transporte = "Transporte";
    public const string Salario = "Salário";
    public const string Vendas = "Vendas";
    public const string Aluguel = "Aluguel";
    public const string Servicos = "Serviços";
    public const string Impostos = "Impostos";
    public const string Outros = "Outros";

    public static readonly string[] Todas = [
        Alimentacao, Transporte, Salario, Vendas, Aluguel, Servicos, Impostos, Outros
    ];

    public static bool EhValida(string? categoria) =>
        !string.IsNullOrEmpty(categoria) && Todas.Contains(categoria);
}
