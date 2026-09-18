using FluxoDeCaixa.Api.DTOs;
using FluxoDeCaixa.Application.Comandos;
using FluxoDeCaixa.Application.Consultas;
using MediatR;
using FluxoDeCaixa.Api.Extensoes;
using Microsoft.AspNetCore.Mvc;

namespace FluxoDeCaixa.Api.Endpoints;

public static class LancamentosEndpoints
{
    public static IEndpointRouteBuilder MapLancamentosEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/lancamentos")
            .WithTags("Lançamentos")
            .RequireAuthorization("comerciante");

        // POST /lancamentos — per D-06, ENTR-01
        group.MapPost("", async (
            [FromBody] RegistrarLancamentoRequest request,
            HttpContext httpContext,
            [FromServices] IMediator mediator) =>
        {
            var comercianteId = httpContext.User.ObterComercianteId();

            var comando = new RegistrarLancamento(
                ComercianteId: comercianteId,
                Valor: request.Valor,
                Tipo: request.Tipo,
                Data: request.Data,
                Descricao: request.Descricao,
                Categoria: request.Categoria
            );

            var id = await mediator.Send(comando);
            return Results.Created($"/lancamentos/{id}", new { id });
        })
        .WithName("RegistrarLancamento")
        .Produces(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .WithOpenApi();

        // GET /lancamentos?data=2024-01-15&pagina=1&tamanhoPagina=20 — per D-06, D-07, ENTR-02
        group.MapGet("", async (
            [FromQuery(Name = "data")] DateOnly data,
            [FromQuery(Name = "pagina")] int pagina,
            [FromQuery(Name = "tamanhoPagina")] int tamanhoPagina,
            HttpContext httpContext,
            [FromServices] IMediator mediator) =>
        {
            var comercianteId = httpContext.User.ObterComercianteId();

            if (pagina <= 0) pagina = 1;
            if (tamanhoPagina <= 0) tamanhoPagina = 20;

            var consulta = new ConsultarLancamentos(comercianteId, data, pagina, tamanhoPagina);
            var resultado = await mediator.Send(consulta);

            return Results.Ok(new
            {
                resultado.Itens,
                resultado.Pagina,
                resultado.TamanhoPagina,
                resultado.Total,
                resultado.TotalPaginas
            });
        })
        .WithName("ConsultarLancamentos")
        .Produces(StatusCodes.Status200OK)
        .WithOpenApi();

        return app;
    }
}
