using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace FluxoDeCaixa.Web.Services;

/// <summary>
/// Anexa o access token do Keycloak nas chamadas a API. O token nunca e lido
/// nem guardado por codigo nosso: quem o mantem e renova e a biblioteca de
/// autenticacao do Blazor, em memoria (nao em localStorage).
/// </summary>
public class ApiAuthorizationMessageHandler : AuthorizationMessageHandler
{
    public ApiAuthorizationMessageHandler(
        IAccessTokenProvider provider,
        NavigationManager navigation,
        IConfiguration configuration)
        : base(provider, navigation)
    {
        var apiBaseUrl = configuration.GetValue<string>("ApiBaseUrl") ?? "http://localhost:5000";

        // So envia o token para a propria API — evita vazar credencial
        // para qualquer outra origem que o app venha a chamar.
        ConfigureHandler(authorizedUrls: new[] { apiBaseUrl });
    }
}
