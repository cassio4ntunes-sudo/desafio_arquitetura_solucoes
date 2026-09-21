using FluxoDeCaixa.Application.Comandos;
using FluxoDeCaixa.Domain.ObjetosDeValor;
using FluentValidation;

namespace FluxoDeCaixa.Application.Validacao;

public class RegistrarLancamentoValidator : AbstractValidator<RegistrarLancamento>
{
    private static readonly string[] TiposValidos = ["Debito", "Credito"];

    public RegistrarLancamentoValidator()
    {
        // Descrição é opcional: o formulário a rotula como tal, o DTO e o evento
        // de domínio a declaram anulável. O que importa para o saldo é valor,
        // tipo e data — a descrição é conveniência do comerciante.
        RuleFor(x => x.Descricao)
            .MaximumLength(500)
            .WithMessage("Descrição deve ter no máximo 500 caracteres");

        RuleFor(x => x.Valor)
            .GreaterThan(0)
            .WithMessage("Valor deve ser maior que zero");

        RuleFor(x => x.Valor)
            .Must(v => decimal.Round(v, 2) == v)
            .WithMessage("Valor deve ter no máximo 2 casas decimais");

        RuleFor(x => x.Tipo)
            .NotEmpty()
            .WithMessage("Tipo é obrigatório")
            .Must(t => TiposValidos.Contains(t))
            .WithMessage("Tipo deve ser 'Debito' ou 'Credito'");

        RuleFor(x => x.Data)
            .NotEmpty()
            .WithMessage("Data é obrigatória");

        RuleFor(x => x.Categoria)
            .NotEmpty()
            .WithMessage("Categoria é obrigatória")
            .Must(CategoriaLancamento.EhValida)
            .WithMessage($"Categoria deve ser uma das: {string.Join(", ", CategoriaLancamento.Todas)}");

        RuleFor(x => x.ComercianteId)
            .NotEmpty()
            .WithMessage("ComercianteId é obrigatório");
    }
}
