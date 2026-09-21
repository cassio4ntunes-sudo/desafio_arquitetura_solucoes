using FluxoDeCaixa.Application.Interfaces;
using FluxoDeCaixa.Domain.Eventos;
using MediatR;

namespace FluxoDeCaixa.Application.Comandos;

public class RegistrarLancamentoHandler : IRequestHandler<RegistrarLancamento, Guid>
{
    private readonly ILancamentoEventStore _eventStore;
    private readonly IEventoPublicador _publicador;

    public RegistrarLancamentoHandler(
        ILancamentoEventStore eventStore,
        IEventoPublicador publicador)
    {
        _eventStore = eventStore;
        _publicador = publicador;
    }

    public async Task<Guid> Handle(RegistrarLancamento comando, CancellationToken ct)
    {
        var lancamentoId = Guid.NewGuid();
        var evento = new LancamentoRegistrado(
            LancamentoId: lancamentoId,
            ComercianteId: comando.ComercianteId,
            Valor: comando.Valor,
            Tipo: comando.Tipo,
            Moeda: "BRL",
            Descricao: comando.Descricao,
            Categoria: comando.Categoria,
            Data: comando.Data,
            RegistradoEm: DateTimeOffset.UtcNow   // D-08: UTC
        );

        // 1. Persist event in Marten (source of truth)
        await _eventStore.RegistrarAsync(evento, ct);

        // 2. Publish to RabbitMQ via MassTransit (best-effort, per D-04)
        await _publicador.PublicarLancamentoRegistradoAsync(evento, ct);

        return lancamentoId;
    }
}
