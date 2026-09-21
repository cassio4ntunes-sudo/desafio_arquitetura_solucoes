using FluentAssertions;
using FluxoDeCaixa.Application.Comandos;
using FluxoDeCaixa.Application.Validacao;

namespace FluxoDeCaixa.Tests.Unit.Application;

public class RegistrarLancamentoValidatorTests
{
    private readonly RegistrarLancamentoValidator _validator = new();

    [Fact]
    public void Comando_valido_deve_passar()
    {
        var comando = new RegistrarLancamento(
            ComercianteId: Guid.NewGuid(),
            Valor: 100m, Tipo: "Credito",
            Data: new DateOnly(2024, 1, 15), Descricao: "Teste",
            Categoria: "Vendas");

        var result = _validator.Validate(comando);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Debito")]
    [InlineData("Credito")]
    public void Tipos_validos_devem_passar(string tipo)
    {
        var comando = new RegistrarLancamento(
            ComercianteId: Guid.NewGuid(),
            Valor: 50m, Tipo: tipo,
            Data: new DateOnly(2024, 1, 1), Descricao: "Teste",
            Categoria: "Vendas");

        var result = _validator.Validate(comando);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Valor_zero_deve_falhar()
    {
        var comando = new RegistrarLancamento(
            ComercianteId: Guid.NewGuid(),
            Valor: 0m, Tipo: "Debito",
            Data: new DateOnly(2024, 1, 1), Descricao: null,
            Categoria: "Vendas");

        var result = _validator.Validate(comando);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("maior que zero"));
    }

    [Fact]
    public void Valor_negativo_deve_falhar()
    {
        var comando = new RegistrarLancamento(
            ComercianteId: Guid.NewGuid(),
            Valor: -10m, Tipo: "Debito",
            Data: new DateOnly(2024, 1, 1), Descricao: null,
            Categoria: "Vendas");

        var result = _validator.Validate(comando);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("maior que zero"));
    }

    [Fact]
    public void Valor_com_tres_casas_decimais_deve_falhar()
    {
        var comando = new RegistrarLancamento(
            ComercianteId: Guid.NewGuid(),
            Valor: 10.123m, Tipo: "Credito",
            Data: new DateOnly(2024, 1, 1), Descricao: null,
            Categoria: "Vendas");

        var result = _validator.Validate(comando);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("2 casas decimais"));
    }

    [Fact]
    public void Tipo_vazio_deve_falhar()
    {
        var comando = new RegistrarLancamento(
            ComercianteId: Guid.NewGuid(),
            Valor: 100m, Tipo: "",
            Data: new DateOnly(2024, 1, 1), Descricao: null,
            Categoria: "Vendas");

        var result = _validator.Validate(comando);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Tipo_invalido_deve_falhar()
    {
        var comando = new RegistrarLancamento(
            ComercianteId: Guid.NewGuid(),
            Valor: 100m, Tipo: "Invalido",
            Data: new DateOnly(2024, 1, 1), Descricao: null,
            Categoria: "Vendas");

        var result = _validator.Validate(comando);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorMessage.Contains("Debito") || e.ErrorMessage.Contains("Credito"));
    }

    // A descrição é opcional — o formulário a rotula como tal e o evento de
    // domínio a declara anulável. O que define o saldo é valor, tipo e data.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Descricao_opcional_deve_passar(string? descricao)
    {
        var comando = new RegistrarLancamento(
            ComercianteId: Guid.NewGuid(),
            Valor: 100m, Tipo: "Credito",
            Data: new DateOnly(2024, 1, 1), Descricao: descricao,
            Categoria: "Vendas");

        var result = _validator.Validate(comando);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Descricao_acima_de_500_caracteres_deve_falhar()
    {
        var comando = new RegistrarLancamento(
            ComercianteId: Guid.NewGuid(),
            Valor: 100m, Tipo: "Credito",
            Data: new DateOnly(2024, 1, 1), Descricao: new string('a', 501),
            Categoria: "Vendas");

        var result = _validator.Validate(comando);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("500"));
    }
}
