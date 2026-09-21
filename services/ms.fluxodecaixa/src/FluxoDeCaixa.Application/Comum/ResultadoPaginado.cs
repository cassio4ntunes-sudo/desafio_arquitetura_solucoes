namespace FluxoDeCaixa.Application.Comum;

public record ResultadoPaginado<T>(
    IReadOnlyList<T> Itens,
    int Pagina,
    int TamanhoPagina,
    int Total
)
{
    public int TotalPaginas => (int)Math.Ceiling((double)Total / TamanhoPagina);
}
