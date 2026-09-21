using Carrefour.WKR.Consolidacao.Tests.Fixtures;
using FluentAssertions;
using FluxoDeCaixa.Consolidacao.Repositorios;
using FluxoDeCaixa.Domain.Eventos;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace Carrefour.WKR.Consolidacao.Tests.Integracao;

/// <summary>
/// Responsabilidade ponta a ponta deste worker: uma mensagem chega na fila e o
/// read model é materializado.
///
/// O teste publica no barramento <b>sem envolver o serviço de lançamentos</b> —
/// o worker não conhece quem produziu o evento, apenas o contrato. É assim que
/// ele se comporta em produção, e é por isso que os dois podem ser implantados
/// de forma independente (ADR-15).
/// </summary>
[Collection("Worker")]
public class ConsumoDaFilaTests
{
    private readonly WorkerTestFixture _fixture;

    public ConsumoDaFilaTests(WorkerTestFixture fixture)
    {
        _fixture = fixture;
    }

    private static LancamentoRegistrado Evento(Guid comercianteId, DateOnly data, decimal valor, string tipo)
        => new(
            LancamentoId: Guid.NewGuid(),
            ComercianteId: comercianteId,
            Valor: valor,
            Tipo: tipo,
            Moeda: "BRL",
            Descricao: "Publicado direto no barramento",
            Categoria: "Vendas",
            Data: data,
            RegistradoEm: DateTimeOffset.UtcNow);

    /// <summary>Aguarda a consolidação assíncrona refletir o esperado.</summary>
    private async Task<FluxoDeCaixa.Consolidacao.Modelos.ConsolidadoDiario?> AguardarConsolidadoAsync(
        Guid comercianteId, DateOnly data, int quantidadeEsperada, TimeSpan timeout)
    {
        var repo = new PostgresConsolidadoRepository(_fixture.ConnectionString);
        var limite = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < limite)
        {
            var consolidado = await repo.ObterPorDataAsync(comercianteId, data, CancellationToken.None);
            if (consolidado is not null && consolidado.Quantidade >= quantidadeEsperada)
                return consolidado;

            await Task.Delay(250);
        }

        return null;
    }

    [Fact]
    public async Task Mensagem_na_fila_materializa_o_consolidado()
    {
        // Arrange
        var bus = _fixture.Services.GetRequiredService<IBus>();
        var comercianteId = Guid.NewGuid();
        var data = DateOnly.FromDateTime(DateTime.Today.AddDays(-90));

        // Act — publica como o serviço de lançamentos faria
        await bus.Publish(Evento(comercianteId, data, 250.50m, "Credito"));

        // Assert
        var consolidado = await AguardarConsolidadoAsync(comercianteId, data, 1, TimeSpan.FromSeconds(20));

        consolidado.Should().NotBeNull("o worker deveria ter consumido a mensagem");
        consolidado!.TotalCreditos.Should().Be(250.50m);
        consolidado.SaldoLiquido.Should().Be(250.50m);
        consolidado.Quantidade.Should().Be(1);
    }

    [Fact]
    public async Task Creditos_e_debitos_sao_somados_no_saldo_liquido()
    {
        var bus = _fixture.Services.GetRequiredService<IBus>();
        var comercianteId = Guid.NewGuid();
        var data = DateOnly.FromDateTime(DateTime.Today.AddDays(-91));

        await bus.Publish(Evento(comercianteId, data, 300.00m, "Credito"));
        await bus.Publish(Evento(comercianteId, data, 100.00m, "Debito"));

        var consolidado = await AguardarConsolidadoAsync(comercianteId, data, 2, TimeSpan.FromSeconds(20));

        consolidado.Should().NotBeNull();
        consolidado!.TotalCreditos.Should().Be(300.00m);
        consolidado.TotalDebitos.Should().Be(100.00m);
        consolidado.SaldoLiquido.Should().Be(200.00m);
    }

    [Fact]
    public async Task Consolidado_e_escopado_por_comerciante()
    {
        var bus = _fixture.Services.GetRequiredService<IBus>();
        var comercianteA = Guid.NewGuid();
        var comercianteB = Guid.NewGuid();
        var data = DateOnly.FromDateTime(DateTime.Today.AddDays(-92));

        await bus.Publish(Evento(comercianteA, data, 1000.00m, "Credito"));
        await bus.Publish(Evento(comercianteB, data, 7.00m, "Credito"));

        var consolidadoA = await AguardarConsolidadoAsync(comercianteA, data, 1, TimeSpan.FromSeconds(20));
        var consolidadoB = await AguardarConsolidadoAsync(comercianteB, data, 1, TimeSpan.FromSeconds(20));

        consolidadoA!.TotalCreditos.Should().Be(1000.00m);
        consolidadoB!.TotalCreditos.Should().Be(7.00m);
    }
}
