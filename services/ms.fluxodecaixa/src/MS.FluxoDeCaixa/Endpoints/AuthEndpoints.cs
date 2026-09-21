using FluxoDeCaixa.Domain.ObjetosDeValor;

namespace FluxoDeCaixa.Api.Endpoints;

public static class AuthEndpoints
{
    /// <summary>
    /// Autenticacao e registro de comerciante sao responsabilidade do Keycloak
    /// (Authorization Code + PKCE). A API nao emite token nem armazena senha.
    /// Resta aqui apenas metadado de dominio, publico por natureza.
    /// </summary>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth")
            .WithTags("Metadados")
            .RequireRateLimiting("auth");

        group.MapGet("/categorias", () => Results.Ok(CategoriaLancamento.Todas))
            .WithName("ListarCategorias")
            .AllowAnonymous()
            .WithOpenApi();

        return app;
    }
}
