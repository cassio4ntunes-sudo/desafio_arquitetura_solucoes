using FluentAssertions;
using FluxoDeCaixa.Consolidacao.Consumidores;
using FluxoDeCaixa.Consolidacao.Repositorios;
using FluxoDeCaixa.Domain.Eventos;
using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FluxoDeCaixa.Tests.Unit.Consolidacao;

public class LancamentoRegistradoConsumerTests
{
    private readonly IConsolidadoRepository _repository = Substitute.For<IConsolidadoRepository>();
    private readonly ILogger<LancamentoRegistradoConsumer> _logger = Substitute.For<ILogger<LancamentoRegistradoConsumer>>();
    private readonly LancamentoRegistradoConsumer _consumer;

    public LancamentoRegistradoConsumerTests()
    {
        _consumer = new LancamentoRegistradoConsumer(_repository, _logger);
    }

    [Fact]
    public async Task Deve_chamar_upsert_com_evento_de_credito()
    {
        var evento = new LancamentoRegistrado(
            LancamentoId: Guid.NewGuid(),
            ComercianteId: Guid.NewGuid(),
            Valor: 150.50m,
            Tipo: "Credito",
            Moeda: "BRL",
            Descricao: "Venda de produto",
            Categoria: "Vendas",
            Data: new DateOnly(2026, 4, 14),
            RegistradoEm: DateTimeOffset.UtcNow
        );

        var context = Substitute.For<ConsumeContext<LancamentoRegistrado>>();
        context.Message.Returns(evento);
        context.CancellationToken.Returns(CancellationToken.None);

        await _consumer.Consume(context);

        await _repository.Received(1)
            .UpsertAsync(evento, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deve_chamar_upsert_com_evento_de_debito()
    {
        var evento = new LancamentoRegistrado(
            LancamentoId: Guid.NewGuid(),
            ComercianteId: Guid.NewGuid(),
            Valor: 75.00m,
            Tipo: "Debito",
            Moeda: "BRL",
            Descricao: "Pagamento fornecedor",
            Categoria: "Serviços",
            Data: new DateOnly(2026, 4, 15),
            RegistradoEm: DateTimeOffset.UtcNow
        );

        var context = Substitute.For<ConsumeContext<LancamentoRegistrado>>();
        context.Message.Returns(evento);
        context.CancellationToken.Returns(CancellationToken.None);

        await _consumer.Consume(context);

        await _repository.Received(1)
            .UpsertAsync(evento, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deve_propagar_excecao_do_repository()
    {
        var evento = new LancamentoRegistrado(
            LancamentoId: Guid.NewGuid(),
            ComercianteId: Guid.NewGuid(),
            Valor: 100m,
            Tipo: "Credito",
            Moeda: "BRL",
            Descricao: null,
            Categoria: "Outros",
            Data: new DateOnly(2026, 4, 14),
            RegistradoEm: DateTimeOffset.UtcNow
        );

        var context = Substitute.For<ConsumeContext<LancamentoRegistrado>>();
        context.Message.Returns(evento);
        context.CancellationToken.Returns(CancellationToken.None);

        _repository.UpsertAsync(evento, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        var act = () => _consumer.Consume(context);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Connection failed");
    }
}
