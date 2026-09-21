using FluentAssertions;
using FluxoDeCaixa.Domain.Agregados;
using FluxoDeCaixa.Domain.Eventos;

namespace FluxoDeCaixa.Tests.Unit.Domain;

public class LancamentoTests
{
    [Fact]
    public void Create_deve_mapear_todos_os_campos_do_evento()
    {
        var id = Guid.NewGuid();
        var data = new DateOnly(2024, 6, 15);
        var registradoEm = DateTimeOffset.UtcNow;

        var evento = new LancamentoRegistrado(
            LancamentoId: id,
            ComercianteId: Guid.NewGuid(),
            Valor: 250.00m,
            Tipo: "Credito",
            Moeda: "BRL",
            Descricao: "Recebimento PIX",
            Categoria: "Vendas",
            Data: data,
            RegistradoEm: registradoEm
        );

        var lancamento = Lancamento.Create(evento);

        lancamento.Id.Should().Be(id);
        lancamento.ComercianteId.Should().Be(evento.ComercianteId);
        lancamento.Valor.Should().Be(250.00m);
        lancamento.Tipo.Should().Be("Credito");
        lancamento.Moeda.Should().Be("BRL");
        lancamento.Descricao.Should().Be("Recebimento PIX");
        lancamento.Categoria.Should().Be("Vendas");
        lancamento.Data.Should().Be(data);
        lancamento.RegistradoEm.Should().Be(registradoEm);
    }

    [Fact]
    public void Create_deve_aceitar_descricao_nula()
    {
        var evento = new LancamentoRegistrado(
            LancamentoId: Guid.NewGuid(),
            ComercianteId: Guid.NewGuid(),
            Valor: 100m,
            Tipo: "Debito",
            Moeda: "BRL",
            Descricao: null,
            Categoria: "Outros",
            Data: new DateOnly(2024, 1, 1),
            RegistradoEm: DateTimeOffset.UtcNow
        );

        var lancamento = Lancamento.Create(evento);

        lancamento.Descricao.Should().BeNull();
    }
}
