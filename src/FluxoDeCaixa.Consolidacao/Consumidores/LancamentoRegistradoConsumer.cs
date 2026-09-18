using FluxoDeCaixa.Consolidacao.Repositorios;
using FluxoDeCaixa.Domain.Eventos;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace FluxoDeCaixa.Consolidacao.Consumidores;

public class LancamentoRegistradoConsumer : IConsumer<LancamentoRegistrado>
{
    private readonly IConsolidadoRepository _repository;
    private readonly ILogger<LancamentoRegistradoConsumer> _logger;

    public LancamentoRegistradoConsumer(
        IConsolidadoRepository repository,
        ILogger<LancamentoRegistradoConsumer> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<LancamentoRegistrado> context)
    {
        var evento = context.Message;

        _logger.LogInformation(
            "Consolidando lançamento {LancamentoId} para data {Data}",
            evento.LancamentoId, evento.Data);

        await _repository.UpsertAsync(evento, context.CancellationToken);
    }
}
