using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluxoDeCaixa.Web.Models;

namespace FluxoDeCaixa.Web.Services;

public class FluxoDeCaixaApiClient
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public FluxoDeCaixaApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<RegistrarLancamentoResponse> RegistrarLancamentoAsync(RegistrarLancamentoRequest request)
    {
        var payload = new
        {
            valor = request.Valor,
            tipo = request.Tipo,
            data = request.Data.ToString("yyyy-MM-dd"),
            descricao = request.Descricao,
            categoria = request.Categoria
        };

        var response = await _http.PostAsJsonAsync("/lancamentos", payload);

        if (!response.IsSuccessStatusCode)
            throw new ApiException(await LerMensagemDeErroAsync(response));

        var result = await response.Content.ReadFromJsonAsync<RegistrarLancamentoResponse>(JsonOptions);
        return result!;
    }

    /// <summary>
    /// A API responde em ProblemDetails (RFC 7807) com os campos que falharam.
    /// Sem isto, EnsureSuccessStatusCode descartaria o corpo e o usuário veria
    /// apenas "400 Bad Request", sem saber o que corrigir.
    /// </summary>
    private static async Task<string> LerMensagemDeErroAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return "Sessão expirada. Faça login novamente.";

        try
        {
            var problema = await response.Content.ReadFromJsonAsync<ProblemaValidacao>(JsonOptions);

            if (problema?.Errors is { Count: > 0 })
                return string.Join(" ", problema.Errors.Select(e => e.ErrorMessage));

            if (!string.IsNullOrWhiteSpace(problema?.Title))
                return problema!.Title!;
        }
        catch (JsonException)
        {
            // corpo não é ProblemDetails — cai no genérico abaixo
        }

        return $"A requisição falhou ({(int)response.StatusCode}).";
    }

    private record ProblemaValidacao(string? Title, List<ErroDeCampo>? Errors);
    private record ErroDeCampo(string? PropertyName, string? ErrorMessage);

    public async Task<PaginatedResult<LancamentoResponse>> ConsultarLancamentosAsync(
        DateOnly data, int pagina = 1, int tamanhoPagina = 20)
    {
        var url = $"/lancamentos?data={data:yyyy-MM-dd}&pagina={pagina}&tamanhoPagina={tamanhoPagina}";
        var result = await _http.GetFromJsonAsync<PaginatedResult<LancamentoResponse>>(url, JsonOptions);
        return result ?? new PaginatedResult<LancamentoResponse>();
    }

    public async Task<ConsolidadoDiarioResponse?> ConsultarConsolidadoAsync(DateOnly data)
    {
        var url = $"/consolidado/diario?data={data:yyyy-MM-dd}";
        var response = await _http.GetAsync(url);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ConsolidadoDiarioResponse>(JsonOptions);
    }
}
