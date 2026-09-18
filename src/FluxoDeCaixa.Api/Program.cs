using System.Threading.RateLimiting;
using FluxoDeCaixa.Api.Endpoints;
using FluxoDeCaixa.Api.Middleware;
using FluxoDeCaixa.Api.Saude;
using FluxoDeCaixa.Application;
using FluxoDeCaixa.Consolidacao;
using FluxoDeCaixa.Consolidacao.Consumidores;
using FluxoDeCaixa.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Formatting.Json;

var builder = WebApplication.CreateBuilder(args);

// Serilog structured logging
builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(new JsonFormatter()));

// Application layer (MediatR + FluentValidation pipeline)
builder.Services.AddApplicationServices();

// Infrastructure layer (Marten + MassTransit + interface implementations)
builder.Services.AddInfrastructureServices(builder.Configuration, bus =>
{
    bus.AddConsumer<LancamentoRegistradoConsumer>();
});

// Consolidação layer (consumer + read model store — per D-12)
builder.Services.AddConsolidacaoServices(builder.Configuration);

// OpenAPI (per API-02) — geração nativa do .NET 10 (Microsoft.AspNetCore.OpenApi).
// O Swashbuckle foi removido na migração: a versão 6.x é binariamente incompatível
// com o Microsoft.OpenApi 2.x que o ASP.NET Core 10 traz, e a geração nativa cobre
// o mesmo caso de uso sem dependência extra.
builder.Services.AddOpenApi();

// Problem Details for structured errors (RFC 7807)
builder.Services.AddProblemDetails();

// CORS — origens explícitas por configuração. Sem AllowAnyOrigin: a API é
// autenticada por JWT e só o frontend conhecido deve chamá-la do browser.
var origensPermitidas = builder.Configuration
    .GetSection("Cors:OrigensPermitidas").Get<string[]>()
    ?? new[] { "http://localhost:5010", "https://localhost:5011" };

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(origensPermitidas)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("X-Correlation-Id");
    });
});

// Autenticação — a API é um RESOURCE SERVER. Não emite token, não armazena
// senha e não conhece credencial: apenas valida a assinatura RS256 do token
// emitido pelo Keycloak, buscando a chave pública no JWKS do realm.
// Identidade é capacidade genérica (ver docs/DOMINIOS-E-CAPACIDADES.md):
// delegada a um IdP, não construída aqui.
var keycloakAuthority = builder.Configuration["Keycloak:Authority"]
    ?? throw new InvalidOperationException(
        "Keycloak:Authority não configurado. Ex.: http://localhost:8080/realms/fluxocaixa. " +
        "Veja o README, seção Configuração.");

var keycloakAudience = builder.Configuration["Keycloak:Audience"] ?? "fluxocaixa-api";
var keycloakMetadata = builder.Configuration["Keycloak:MetadataAddress"];
var requireHttpsMetadata = builder.Configuration.GetValue("Keycloak:RequireHttpsMetadata", true);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Authority é o issuer que o browser enxerga — precisa bater com o claim `iss`.
        options.Authority = keycloakAuthority;
        options.Audience = keycloakAudience;

        // Dentro do Docker o browser e a API alcançam o Keycloak por hostnames
        // diferentes. MetadataAddress aponta para a rota interna, enquanto o
        // issuer validado continua sendo o público.
        if (!string.IsNullOrWhiteSpace(keycloakMetadata))
            options.MetadataAddress = keycloakMetadata;

        // Só false em ambiente local (Keycloak em start-dev, sem TLS).
        options.RequireHttpsMetadata = requireHttpsMetadata;

        // Mantém os nomes originais das claims do OIDC (`sub`, `preferred_username`)
        // em vez do mapeamento legado para URIs do WS-Federation.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = keycloakAuthority,
            ValidateAudience = true,
            ValidAudience = keycloakAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "preferred_username",
            RoleClaimType = "roles"
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Todo endpoint de negócio exige um comerciante autenticado pelo Keycloak.
    options.AddPolicy("comerciante", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("sub"));
});

// Rate limiting — protege contra abuso e picos. O limite do consolidado é
// dimensionado acima do RNF do desafio (50 req/s) para não barrar carga legítima.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("consolidado", limiter =>
    {
        limiter.PermitLimit = 600;                      // 600/10s = 60 req/s
        limiter.Window = TimeSpan.FromSeconds(10);
        limiter.QueueLimit = 100;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = 10;                       // 10/min por instância
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});

// Health checks — liveness (processo vivo) e readiness (dependências prontas)
var connStrHealth = builder.Configuration.GetConnectionString("Marten")
    ?? throw new InvalidOperationException("Missing Marten connection string");

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddCheck("postgres", new PostgresHealthCheck(connStrHealth), tags: new[] { "ready" });

var app = builder.Build();

// Phase 2: Ensure consolidado_diario table exists (idempotent DDL)
await app.Services.EnsureConsolidadoTableAsync();

// Correlation ID middleware (per API-04) — must be first
app.UseMiddleware<CorrelationIdMiddleware>();

// Enable CORS for Blazor WASM frontend
app.UseCors();

// Rate limiting antes da autenticação: barra abuso sem custo de validar token
app.UseRateLimiter();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Global exception handler — ValidationException vira 400 com os erros de campo;
// qualquer outra exceção vira 500 com ProblemDetails (nunca corpo vazio).
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
        context.Response.ContentType = "application/problem+json";

        switch (exceptionFeature?.Error)
        {
            case ValidationException validationException:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new
                {
                    type = "https://tools.ietf.org/html/rfc7807",
                    title = "Erro de validação",
                    status = 400,
                    errors = validationException.Errors
                        .Select(e => new { e.PropertyName, e.ErrorMessage })
                        .ToList()
                });
                break;

            case UnauthorizedAccessException:
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new
                {
                    type = "https://tools.ietf.org/html/rfc7807",
                    title = "Não autorizado",
                    status = 401
                });
                break;

            default:
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new
                {
                    type = "https://tools.ietf.org/html/rfc7807",
                    title = "Erro interno",
                    status = 500,
                    correlationId = context.Response.Headers["X-Correlation-Id"].ToString()
                });
                break;
        }
    });
});

// OpenAPI + Scalar UI (per API-02)
// MapOpenApi serve o documento em /openapi/v1.json — rota que o Scalar já espera.
app.MapOpenApi();
app.MapScalarApiReference();

// Health probes — /health/live para liveness, /health/ready para readiness
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();

// REST endpoints (per D-06)
app.MapAuthEndpoints();
app.MapLancamentosEndpoints();
app.MapConsolidadoEndpoints();

app.Run();

// Make Program accessible for WebApplicationFactory in integration tests
public partial class Program { }
