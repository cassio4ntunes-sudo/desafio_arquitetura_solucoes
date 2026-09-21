using FluentAssertions;
using FluxoDeCaixa.Domain.Eventos;

namespace FluxoDeCaixa.Tests.Unit.Domain;

public class LancamentoRegistradoTests
{
    [Fact]
    public void Deve_criar_evento_com_todas_as_propriedades()
    {
        var id = Guid.NewGuid();
        var data = new DateOnly(2024, 1, 15);
        var registradoEm = DateTimeOffset.UtcNow;

        var evento = new LancamentoRegistrado(
            LancamentoId: id,
            ComercianteId: Guid.NewGuid(),
            Valor: 150.75m,
            Tipo: "Credito",
            Moeda: "BRL",
            Descricao: "Venda no cartão",
            Categoria: "Vendas",
            Data: data,
            RegistradoEm: registradoEm
        );

        evento.LancamentoId.Should().Be(id);
        evento.Valor.Should().Be(150.75m);
        evento.Tipo.Should().Be("Credito");
        evento.Moeda.Should().Be("BRL");
        evento.Descricao.Should().Be("Venda no cartão");
        evento.Categoria.Should().Be("Vendas");
        evento.Data.Should().Be(data);
        evento.RegistradoEm.Should().Be(registradoEm);
    }

    [Fact]
    public void Deve_aceitar_descricao_nula()
    {
        var evento = new LancamentoRegistrado(
            LancamentoId: Guid.NewGuid(),
            ComercianteId: Guid.NewGuid(),
            Valor: 50m,
            Tipo: "Debito",
            Moeda: "BRL",
            Descricao: null,
            Categoria: "Outros",
            Data: new DateOnly(2024, 3, 1),
            RegistradoEm: DateTimeOffset.UtcNow
        );

        evento.Descricao.Should().BeNull();
    }
}
