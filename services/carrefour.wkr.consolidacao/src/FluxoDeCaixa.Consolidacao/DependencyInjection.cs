using FluxoDeCaixa.Consolidacao.Repositorios;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FluxoDeCaixa.Consolidacao;

/// <summary>
/// Cópia desta biblioteca pertencente ao <b>Carrefour.WKR.Consolidacao</b>.
///
/// Contém apenas o lado de ESCRITA do read model — consumer e repositório.
/// As consultas (<c>ConsultarConsolidadoDiario</c>) vivem na cópia do
/// Carrefour.MS.FluxoDeCaixa, que é quem atende o comerciante.
///
/// Esta separação é intencional: em multi-repo cada serviço carrega apenas o
/// código que executa, e não a biblioteca inteira.
/// </summary>
public static class ConsolidacaoServiceExtensions
{
    /// <summary>
    /// Repositório do read model. O UPSERT é idempotente (ADR-10): reprocessar
    /// a mesma mensagem não soma o valor duas vezes.
    /// </summary>
    public static IServiceCollection AddConsolidadoRepositorio(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IConsolidadoRepository>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            return new PostgresConsolidadoRepository(config.GetConnectionString("Marten")!);
        });

        return services;
    }

    /// <summary>
    /// DDL idempotente do read model e do inbox de idempotência.
    ///
    /// O worker é o <b>dono da escrita</b> deste schema. O MS também executa o
    /// mesmo DDL na sua cópia, de propósito: ele não pode depender do worker
    /// estar no ar para subir — seria o acoplamento que o RNF proíbe.
    ///
    /// Em produção isto sai do startup e vira step de migrations no pipeline
    /// (ver docs/ARQUITETURA-ALVO.md).
    /// </summary>
    public static async Task EnsureConsolidadoTableAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IConsolidadoRepository>();
        await repository.CriarTabelaSeNaoExisteAsync(CancellationToken.None);
    }
}
