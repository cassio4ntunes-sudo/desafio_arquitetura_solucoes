using FluentAssertions;
using FluxoDeCaixa.Domain.ObjetosDeValor;

namespace FluxoDeCaixa.Tests.Unit.Domain;

public class DinheiroTests
{
    [Fact]
    public void Deve_criar_com_valor_e_moeda_padrao()
    {
        var dinheiro = new Dinheiro(100.50m);

        dinheiro.Valor.Should().Be(100.50m);
        dinheiro.Moeda.Should().Be("BRL");
    }

    [Fact]
    public void Deve_aceitar_valor_zero()
    {
        var dinheiro = new Dinheiro(0m);

        dinheiro.Valor.Should().Be(0m);
    }

    [Fact]
    public void Deve_rejeitar_valor_negativo()
    {
        var act = () => new Dinheiro(-1m);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*negativo*");
    }

    [Fact]
    public void Deve_rejeitar_mais_de_duas_casas_decimais()
    {
        var act = () => new Dinheiro(10.123m);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*2 casas decimais*");
    }

    [Fact]
    public void Deve_criar_com_moeda_especifica()
    {
        var dinheiro = new Dinheiro(10m, "USD");

        dinheiro.Moeda.Should().Be("USD");
    }

    [Fact]
    public void Deve_somar_mesma_moeda()
    {
        var a = new Dinheiro(10.50m);
        var b = new Dinheiro(20.30m);

        var resultado = a + b;

        resultado.Valor.Should().Be(30.80m);
        resultado.Moeda.Should().Be("BRL");
    }

    [Fact]
    public void Deve_rejeitar_soma_moedas_diferentes()
    {
        var brl = new Dinheiro(10m, "BRL");
        var usd = new Dinheiro(5m, "USD");

        var act = () => brl + usd;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Moedas diferentes*");
    }

    [Fact]
    public void Zero_deve_ter_valor_zero_e_moeda_brl()
    {
        var zero = Dinheiro.Zero();

        zero.Valor.Should().Be(0m);
        zero.Moeda.Should().Be("BRL");
    }

    [Fact]
    public void Deve_ser_igual_com_mesmo_valor_e_moeda()
    {
        var a = new Dinheiro(50.00m, "BRL");
        var b = new Dinheiro(50.00m, "BRL");

        a.Should().Be(b);
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(0.10)]
    [InlineData(1.00)]
    [InlineData(99999.99)]
    public void Deve_aceitar_valores_com_ate_duas_casas(decimal valor)
    {
        var dinheiro = new Dinheiro(valor);

        dinheiro.Valor.Should().Be(valor);
    }
}
