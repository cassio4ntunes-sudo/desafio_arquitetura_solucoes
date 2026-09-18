using FluxoDeCaixa.Application.Comandos;
using FluxoDeCaixa.Application.Comportamentos;
using FluxoDeCaixa.Application.Validacao;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace FluxoDeCaixa.Application;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<RegistrarLancamentoHandler>());

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidacaoBehavior<,>));
        services.AddScoped<IValidator<RegistrarLancamento>, RegistrarLancamentoValidator>();

        return services;
    }
}
