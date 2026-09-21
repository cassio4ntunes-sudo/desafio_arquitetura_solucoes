using FluxoDeCaixa.Consolidacao.Consultas;
using FluxoDeCaixa.Consolidacao.Repositorios;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FluxoDeCaixa.Consolidacao;

/// <summary>
/// Esta biblioteca é compartilhada por dois hosts com papéis distintos:
///
///   • MS.FluxoDeCaixa  — LÊ o consolidado (query)
///   • WKR.Consolidacao — ESCREVE o consolidado (consumer)
///
/// Por isso o registro é granular: cada serviço compõe apenas o que usa.
/// Registrar o consumer no MS reintroduziria o acoplamento que o RNF proíbe.
/// </summary>
public static class ConsolidacaoServiceExtensions
{
    /// <summary>
    /// Repositório do read model. Necessário nos dois serviços — um para ler,
    /// outro para escrever. A connection string é resolvida preguiçosamente
    /// para que sobrescritas de configuração em teste sejam visíveis.
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
    /// Lado de LEITURA — handlers MediatR das consultas. Só o MS precisa.
    /// </summary>
    public static IServiceCollection AddConsolidadoConsultas(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<ConsultarConsolidadoDiarioHandler>());

        return services;
    }

    /// <summary>
    /// DDL idempotente do read model e do inbox de idempotência.
    ///
    /// Os dois serviços chamam este método na inicialização, de propósito: o MS
    /// não pode depender do WKR estar no ar para subir — seria exatamente o
    /// acoplamento que o RNF do desafio proíbe. Como o DDL é `IF NOT EXISTS`,
    /// executá-lo nos dois é inofensivo.
    ///
    /// Em produção isto sai do startup e vira step de migrations no pipeline
    /// (ver docs/ARQUITETURA-ALVO.md): com várias instâncias subindo em
    /// paralelo, startup não é lugar de DDL.
    /// </summary>
    public static async Task EnsureConsolidadoTableAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IConsolidadoRepository>();
        await repository.CriarTabelaSeNaoExisteAsync(CancellationToken.None);
    }
}
