using FluentAssertions;
using FluxoDeCaixa.Tests.Integration.Fixtures;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace FluxoDeCaixa.Tests.Integration;

/// <summary>
/// Guarda a fronteira entre os serviços (ADR-15).
///
/// Neste repositório o consumer da consolidação <b>nem sequer existe como
/// código</b>: a cópia local de FluxoDeCaixa.Consolidacao não tem a pasta
/// Consumidores. Ainda assim, alguém poderia reintroduzir o acoplamento
/// registrando um consumer no barramento deste serviço — e isso não quebraria
/// nenhum outro teste, porque o fluxo continuaria funcionando.
///
/// Estes testes falham nesse caso.
/// </summary>
[Collection("Integration")]
public class SeparacaoDeServicosTests
{
    private readonly IntegrationTestFixture _fixture;

    public SeparacaoDeServicosTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Servico_de_lancamentos_nao_registra_consumer_algum()
    {
        // Este serviço é PRODUTOR: publica LancamentoRegistrado e segue adiante.
        // Consumir a própria fila faria o registro de lançamentos compartilhar
        // destino com a consolidação — o que o RNF do enunciado proíbe.
        var consumers = _fixture.Factory.Services
            .GetServices<IConsumer>()
            .ToList();

        consumers.Should().BeEmpty(
            "o serviço de lançamentos apenas publica; quem consome é o " +
            "WKR.Consolidacao, que vive em outro repositório (ADR-15)");
    }

    [Fact]
    public void Servico_de_lancamentos_nao_registra_consumer_do_evento_de_lancamento()
    {
        // Verificação específica do contrato publicado por este serviço.
        var consumidoresDoEvento = _fixture.Factory.Services
            .GetServices<IConsumer<Domain.Eventos.LancamentoRegistrado>>()
            .ToList();

        consumidoresDoEvento.Should().BeEmpty(
            "materializar o consolidado é responsabilidade do worker");
    }

    [Fact]
    public void Codigo_do_consumer_nao_faz_parte_deste_repositorio()
    {
        // Em multi-repo, a separação começa no que cada repositório carrega.
        // Este assembly não deve conter nenhuma implementação de IConsumer.
        var assemblyDaConsolidacao = typeof(FluxoDeCaixa.Consolidacao.ConsolidacaoServiceExtensions).Assembly;

        var tiposConsumer = assemblyDaConsolidacao
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false }
                        && t.GetInterfaces().Any(i => i == typeof(IConsumer)
                                                      || (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IConsumer<>))))
            .ToList();

        tiposConsumer.Should().BeEmpty(
            "a cópia local de FluxoDeCaixa.Consolidacao neste repositório contém " +
            "apenas o lado de leitura (Modelos, Repositorios, Consultas). " +
            "Os Consumidores pertencem à cópia do worker");
    }
}
