using FluentAssertions;
using FluxoDeCaixa.Consolidacao.Consultas;
using FluxoDeCaixa.Consolidacao.Modelos;
using FluxoDeCaixa.Consolidacao.Repositorios;
using NSubstitute;

namespace FluxoDeCaixa.Tests.Unit.Consolidacao;

public class ConsultarConsolidadoDiarioHandlerTests
{
    private readonly IConsolidadoRepository _repository = Substitute.For<IConsolidadoRepository>();
    private readonly ConsultarConsolidadoDiarioHandler _handler;
    private readonly Guid _comercianteId = Guid.NewGuid();

    public ConsultarConsolidadoDiarioHandlerTests()
    {
        _handler = new ConsultarConsolidadoDiarioHandler(_repository);
    }

    [Fact]
    public async Task Deve_retornar_consolidado_quando_data_existe()
    {
        var data = new DateOnly(2026, 4, 14);
        var esperado = new ConsolidadoDiario(
            ComercianteId: _comercianteId,
            Data: data,
            TotalCreditos: 1500.00m,
            TotalDebitos: 500.00m,
            SaldoLiquido: 1000.00m,
            Quantidade: 5,
            AtualizadoEm: DateTimeOffset.UtcNow
        );

        _repository.ObterPorDataAsync(_comercianteId, data, Arg.Any<CancellationToken>())
            .Returns(esperado);

        var resultado = await _handler.Handle(
            new ConsultarConsolidadoDiario(_comercianteId, data), CancellationToken.None);

        resultado.Should().BeSameAs(esperado);
        await _repository.Received(1)
            .ObterPorDataAsync(_comercianteId, data, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deve_retornar_consolidado_zerado_quando_dia_sem_movimento()
    {
        var data = new DateOnly(2026, 1, 1);

        _repository.ObterPorDataAsync(_comercianteId, data, Arg.Any<CancellationToken>())
            .Returns((ConsolidadoDiario?)null);

        var resultado = await _handler.Handle(
            new ConsultarConsolidadoDiario(_comercianteId, data), CancellationToken.None);

        // Dia sem lançamento é resposta válida de negócio: saldo zero, não 404.
        resultado.Should().NotBeNull();
        resultado.ComercianteId.Should().Be(_comercianteId);
        resultado.Data.Should().Be(data);
        resultado.TotalCreditos.Should().Be(0m);
        resultado.TotalDebitos.Should().Be(0m);
        resultado.SaldoLiquido.Should().Be(0m);
        resultado.Quantidade.Should().Be(0);
    }

    [Fact]
    public async Task Deve_escopar_consulta_ao_comerciante_do_token()
    {
        var data = new DateOnly(2026, 12, 31);
        var outroComerciante = Guid.NewGuid();

        _repository.ObterPorDataAsync(Arg.Any<Guid>(), data, Arg.Any<CancellationToken>())
            .Returns((ConsolidadoDiario?)null);

        await _handler.Handle(
            new ConsultarConsolidadoDiario(_comercianteId, data), CancellationToken.None);

        // O repositório precisa receber o comerciante da consulta — é o que
        // impede um comerciante de ler o consolidado de outro.
        await _repository.Received(1)
            .ObterPorDataAsync(
                Arg.Is<Guid>(id => id == _comercianteId),
                Arg.Is<DateOnly>(d => d == new DateOnly(2026, 12, 31)),
                Arg.Any<CancellationToken>());

        await _repository.DidNotReceive()
            .ObterPorDataAsync(outroComerciante, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }
}
