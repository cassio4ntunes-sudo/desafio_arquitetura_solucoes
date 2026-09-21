using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using FluxoDeCaixa.Web;
using FluxoDeCaixa.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration.GetValue<string>("ApiBaseUrl") ?? "http://localhost:5000";

builder.Services.AddMudServices();

// Autenticação OIDC contra o Keycloak — Authorization Code + PKCE.
// A SPA é um cliente público: não há client secret, e a senha do comerciante
// jamais passa por este código. O login acontece na página do Keycloak.
builder.Services.AddOidcAuthentication(options =>
{
    builder.Configuration.Bind("Keycloak", options.ProviderOptions);

    // `openid` e `profile` já vêm por padrão — acrescentar de novo duplicaria
    // o parâmetro scope na URL de autorização. Só falta `email`.
    // A audiência da API entra pelo protocol mapper do realm, não por escopo.
    options.ProviderOptions.DefaultScopes.Add("email");

    // Claim que o Blazor usa como nome exibido do usuário.
    options.UserOptions.NameClaim = "preferred_username";
    options.UserOptions.RoleClaim = "roles";
});

// Handler que anexa o access token às chamadas da API. Ao expirar, dispara
// renovação silenciosa; se não for possível, redireciona ao login.
builder.Services.AddScoped<ApiAuthorizationMessageHandler>();

builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<ApiAuthorizationMessageHandler>();
    handler.InnerHandler = new HttpClientHandler();
    return new HttpClient(handler) { BaseAddress = new Uri(apiBaseUrl) };
});

builder.Services.AddScoped<FluxoDeCaixaApiClient>();

await builder.Build().RunAsync();
