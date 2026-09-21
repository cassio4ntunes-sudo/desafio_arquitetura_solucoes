using System.Security.Claims;

namespace FluxoDeCaixa.Api.Extensoes;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Extrai o ComercianteId da claim `sub` do token emitido pelo Keycloak —
    /// o identificador estável do usuário no IdP. Toda consulta e todo comando
    /// são escopados por este id: é a fronteira de isolamento entre comerciantes.
    ///
    /// O valor nunca vem do corpo ou da query string, que o cliente controla.
    /// </summary>
    public static Guid ObterComercianteId(this ClaimsPrincipal usuario)
    {
        // Com MapInboundClaims = false a claim preserva o nome original `sub`;
        // NameIdentifier fica como fallback caso o mapeamento legado seja reativado.
        var valor = usuario.FindFirst("sub")?.Value
                    ?? usuario.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(valor, out var comercianteId))
            throw new UnauthorizedAccessException(
                "Token sem claim 'sub' válida — não foi possível identificar o comerciante.");

        return comercianteId;
    }

    /// <summary>Nome de exibição do comerciante, para log e diagnóstico.</summary>
    public static string? ObterNomeDeUsuario(this ClaimsPrincipal usuario)
        => usuario.FindFirst("preferred_username")?.Value
           ?? usuario.FindFirst("email")?.Value;
}
