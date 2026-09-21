using FluxoDeCaixa.Api.DTOs;
using FluxoDeCaixa.Api.Extensoes;
using FluxoDeCaixa.Consolidacao.Consultas;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FluxoDeCaixa.Api.Endpoints;

public static class ConsolidadoEndpoints
{
    public static IEndpointRouteBuilder MapConsolidadoEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/consolidado")
            .WithTags("Consolidado Diário")
            .RequireAuthorization("comerciante")
            .RequireRateLimiting("consolidado");

        // GET /consolidado/diario?data=2026-04-14 — per D-14, CONS-01
        // Sempre escopado ao comerciante do token: dois comerciantes nunca
        // enxergam o saldo um do outro.
        group.MapGet("/diario", async (
            [FromQuery(Name = "data")] DateOnly data,
            HttpContext httpContext,
            [FromServices] IMediator mediator) =>
        {
            var comercianteId = httpContext.User.ObterComercianteId();

            var consulta = new ConsultarConsolidadoDiario(comercianteId, data);
            var resultado = await mediator.Send(consulta);

            return Results.Ok(new ConsolidadoDiarioResponse(
                Data: resultado.Data,
                TotalCreditos: resultado.TotalCreditos,
                TotalDebitos: resultado.TotalDebitos,
                SaldoLiquido: resultado.SaldoLiquido,
                QuantidadeLancamentos: resultado.Quantidade
            ));
        })
        .WithName("ConsultarConsolidadoDiario")
        .Produces<ConsolidadoDiarioResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .WithOpenApi();

        return app;
    }
}
