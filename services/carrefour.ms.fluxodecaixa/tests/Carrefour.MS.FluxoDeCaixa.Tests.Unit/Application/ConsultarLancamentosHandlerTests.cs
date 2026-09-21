using FluentAssertions;
using FluxoDeCaixa.Application.Comum;
using FluxoDeCaixa.Application.Consultas;
using FluxoDeCaixa.Application.Interfaces;
using FluxoDeCaixa.Domain.ModelosDeLeitura;
using NSubstitute;

namespace FluxoDeCaixa.Tests.Unit.Application;

public class ConsultarLancamentosHandlerTests
{
    private readonly ILancamentoQueryStore _queryStore = Substitute.For<ILancamentoQueryStore>();
    private readonly ConsultarLancamentosHandler _handler;

    public ConsultarLancamentosHandlerTests()
    {
        _handler = new ConsultarLancamentosHandler(_queryStore);
    }

    [Fact]
    public async Task Deve_consultar_query_store_com_parametros_corretos()
    {
        var comercianteId = Guid.NewGuid();
        var data = new DateOnly(2024, 1, 15);
        var consulta = new ConsultarLancamentos(comercianteId, data, Pagina: 2, TamanhoPagina: 10);
        var esperado = new ResultadoPaginado<LancamentoResumo>(
            Itens: new List<LancamentoResumo>(),
            Pagina: 2,
            TamanhoPagina: 10,
            Total: 0
        );

        _queryStore.ConsultarPorDataAsync(comercianteId, data, 2, 10, Arg.Any<CancellationToken>())
            .Returns(esperado);

        var resultado = await _handler.Handle(consulta, CancellationToken.None);

        resultado.Should().BeSameAs(esperado);
        await _queryStore.Received(1)
            .ConsultarPorDataAsync(comercianteId, data, 2, 10, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deve_retornar_resultado_do_query_store()
    {
        var comercianteId = Guid.NewGuid();
        var data = new DateOnly(2024, 6, 1);
        var itens = new List<LancamentoResumo>
        {
            new(Guid.NewGuid(), comercianteId, 100m, "Credito", "BRL", "Venda", "Vendas", data, DateTimeOffset.UtcNow),
            new(Guid.NewGuid(), comercianteId, 50m, "Debito", "BRL", "Compra", "Alimentação", data, DateTimeOffset.UtcNow)
        };
        var esperado = new ResultadoPaginado<LancamentoResumo>(
            Itens: itens, Pagina: 1, TamanhoPagina: 20, Total: 2);

        _queryStore.ConsultarPorDataAsync(comercianteId, data, 1, 20, Arg.Any<CancellationToken>())
            .Returns(esperado);

        var resultado = await _handler.Handle(
            new ConsultarLancamentos(comercianteId, data), CancellationToken.None);

        resultado.Itens.Should().HaveCount(2);
        resultado.Total.Should().Be(2);
        resultado.TotalPaginas.Should().Be(1);
    }
}
