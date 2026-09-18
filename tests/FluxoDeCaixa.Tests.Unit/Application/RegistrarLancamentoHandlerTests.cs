using FluentAssertions;
using FluxoDeCaixa.Application.Comandos;
using FluxoDeCaixa.Application.Interfaces;
using FluxoDeCaixa.Domain.Eventos;
using NSubstitute;

namespace FluxoDeCaixa.Tests.Unit.Application;

public class RegistrarLancamentoHandlerTests
{
    private readonly ILancamentoEventStore _eventStore = Substitute.For<ILancamentoEventStore>();
    private readonly IEventoPublicador _publicador = Substitute.For<IEventoPublicador>();
    private readonly RegistrarLancamentoHandler _handler;

    public RegistrarLancamentoHandlerTests()
    {
        _eventStore.RegistrarAsync(Arg.Any<LancamentoRegistrado>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<LancamentoRegistrado>().LancamentoId);

        _handler = new RegistrarLancamentoHandler(_eventStore, _publicador);
    }

    [Fact]
    public async Task Deve_persistir_evento_no_event_store()
    {
        var comando = CriarComandoValido();

        await _handler.Handle(comando, CancellationToken.None);

        await _eventStore.Received(1)
            .RegistrarAsync(Arg.Any<LancamentoRegistrado>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deve_publicar_evento_no_broker()
    {
        var comando = CriarComandoValido();

        await _handler.Handle(comando, CancellationToken.None);

        await _publicador.Received(1)
            .PublicarLancamentoRegistradoAsync(Arg.Any<LancamentoRegistrado>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Evento_deve_conter_dados_do_comando()
    {
        var comercianteId = Guid.NewGuid();
        var comando = new RegistrarLancamento(
            ComercianteId: comercianteId,
            Valor: 150.75m,
            Tipo: "Credito",
            Data: new DateOnly(2024, 1, 15),
            Descricao: "Venda cartão",
            Categoria: "Vendas"
        );

        await _handler.Handle(comando, CancellationToken.None);

        await _eventStore.Received(1).RegistrarAsync(
            Arg.Is<LancamentoRegistrado>(e =>
                e.ComercianteId == comercianteId &&
                e.Valor == 150.75m &&
                e.Tipo == "Credito" &&
                e.Data == new DateOnly(2024, 1, 15) &&
                e.Descricao == "Venda cartão" &&
                e.Categoria == "Vendas" &&
                e.Moeda == "BRL"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deve_retornar_guid_nao_vazio()
    {
        var comando = CriarComandoValido();

        var resultado = await _handler.Handle(comando, CancellationToken.None);

        resultado.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Evento_deve_ter_registrado_em_proximo_de_utc_now()
    {
        var antes = DateTimeOffset.UtcNow;
        var comando = CriarComandoValido();

        await _handler.Handle(comando, CancellationToken.None);

        var depois = DateTimeOffset.UtcNow;

        await _eventStore.Received(1).RegistrarAsync(
            Arg.Is<LancamentoRegistrado>(e =>
                e.RegistradoEm >= antes && e.RegistradoEm <= depois),
            Arg.Any<CancellationToken>());
    }

    private static RegistrarLancamento CriarComandoValido() => new(
        ComercianteId: Guid.NewGuid(),
        Valor: 100m,
        Tipo: "Debito",
        Data: new DateOnly(2024, 3, 10),
        Descricao: "Compra fornecedor",
        Categoria: "Alimentação"
    );
}
