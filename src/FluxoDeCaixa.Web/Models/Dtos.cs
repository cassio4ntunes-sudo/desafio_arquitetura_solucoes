namespace FluxoDeCaixa.Web.Models;

public record RegistrarLancamentoRequest(decimal Valor, string Tipo, DateOnly Data, string? Descricao, string Categoria);

public record RegistrarLancamentoResponse(Guid Id);

public record LancamentoResponse(
    Guid Id,
    decimal Valor,
    string Tipo,
    string Moeda,
    string? Descricao,
    string Categoria,
    DateOnly Data,
    DateTimeOffset RegistradoEm);

public record ConsolidadoDiarioResponse(
    DateOnly Data,
    decimal TotalCreditos,
    decimal TotalDebitos,
    decimal SaldoLiquido,
    int QuantidadeLancamentos);

public record PaginatedResult<T>
{
    public List<T> Itens { get; init; } = [];
    public int Pagina { get; init; }
    public int TamanhoPagina { get; init; }
    public int Total { get; init; }
    public int TotalPaginas { get; init; }
}

// Auth DTOs
public record RegistrarUsuarioRequest(string Nome, string Email, string Senha);
public record LoginRequest(string Email, string Senha);
public record AuthResponse(string Token, Guid UsuarioId, string Nome);
