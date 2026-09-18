using FluentAssertions;
using FluxoDeCaixa.Consolidacao.Repositorios;
using FluxoDeCaixa.Domain.Eventos;
using FluxoDeCaixa.Tests.Integration.Fixtures;

namespace FluxoDeCaixa.Tests.Integration;

/// <summary>
/// A entrega do RabbitMQ é at-least-once e o MassTransit ainda faz retry
/// (500ms/2s/10s), então o mesmo LancamentoRegistrado pode chegar ao consumer
/// mais de uma vez. Em um razão financeiro isso significaria dinheiro somado
/// em duplicidade. Estes testes provam que aplicar o mesmo evento N vezes
/// produz exatamente o mesmo saldo.
/// </summary>
[Collection("Integration")]
public class IdempotenciaConsolidadoTests
{
    private readonly IntegrationTestFixture _fixture;

    public IdempotenciaConsolidadoTests(IntegrationTestFixture fixture)
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
            Descricao: "Teste idempotência",
            Categoria: "Vendas",
            Data: data,
            RegistradoEm: DateTimeOffset.UtcNow);

    [Fact]
    public async Task Reentrega_do_mesmo_evento_nao_soma_duas_vezes()
    {
        // Arrange
        var repo = new PostgresConsolidadoRepository(_fixture.ConnectionString);
        await repo.CriarTabelaSeNaoExisteAsync(CancellationToken.None);

        var comercianteId = Guid.NewGuid();
        var data = DateOnly.FromDateTime(DateTime.Today.AddDays(-40));
        var evento = Evento(comercianteId, data, 100.00m, "Credito");

        // Act — o mesmo evento chega 5 vezes
        for (var i = 0; i < 5; i++)
            await repo.UpsertAsync(evento, CancellationToken.None);

        // Assert — conta uma única vez
        var consolidado = await repo.ObterPorDataAsync(comercianteId, data, CancellationToken.None);

        consolidado.Should().NotBeNull();
        consolidado!.TotalCreditos.Should().Be(100.00m);
        consolidado.SaldoLiquido.Should().Be(100.00m);
        consolidado.Quantidade.Should().Be(1);
    }

    [Fact]
    public async Task Eventos_distintos_somam_normalmente()
    {
        // Arrange — a idempotência não pode bloquear lançamentos legítimos
        var repo = new PostgresConsolidadoRepository(_fixture.ConnectionString);
        await repo.CriarTabelaSeNaoExisteAsync(CancellationToken.None);

        var comercianteId = Guid.NewGuid();
        var data = DateOnly.FromDateTime(DateTime.Today.AddDays(-41));

        // Act — dois créditos e um débito, todos distintos, cada um entregue 2x
        var credito1 = Evento(comercianteId, data, 300.00m, "Credito");
        var credito2 = Evento(comercianteId, data, 200.00m, "Credito");
        var debito = Evento(comercianteId, data, 50.00m, "Debito");

        foreach (var evento in new[] { credito1, credito2, debito, credito1, credito2, debito })
            await repo.UpsertAsync(evento, CancellationToken.None);

        // Assert
        var consolidado = await repo.ObterPorDataAsync(comercianteId, data, CancellationToken.None);

        consolidado.Should().NotBeNull();
        consolidado!.TotalCreditos.Should().Be(500.00m);
        consolidado.TotalDebitos.Should().Be(50.00m);
        consolidado.SaldoLiquido.Should().Be(450.00m);
        consolidado.Quantidade.Should().Be(3);
    }

    [Fact]
    public async Task Consolidado_e_isolado_por_comerciante()
    {
        // Arrange
        var repo = new PostgresConsolidadoRepository(_fixture.ConnectionString);
        await repo.CriarTabelaSeNaoExisteAsync(CancellationToken.None);

        var comercianteA = Guid.NewGuid();
        var comercianteB = Guid.NewGuid();
        var data = DateOnly.FromDateTime(DateTime.Today.AddDays(-42));

        // Act — mesma data, comerciantes diferentes
        await repo.UpsertAsync(Evento(comercianteA, data, 1000.00m, "Credito"), CancellationToken.None);
        await repo.UpsertAsync(Evento(comercianteB, data, 7.00m, "Credito"), CancellationToken.None);

        // Assert — cada um enxerga apenas o próprio saldo
        var consolidadoA = await repo.ObterPorDataAsync(comercianteA, data, CancellationToken.None);
        var consolidadoB = await repo.ObterPorDataAsync(comercianteB, data, CancellationToken.None);

        consolidadoA!.TotalCreditos.Should().Be(1000.00m);
        consolidadoB!.TotalCreditos.Should().Be(7.00m);
    }
}
