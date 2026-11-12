using MassTransit;
using OrderFlow.Contracts.Abstractions;
using OrderFlow.Worker.Services;

namespace OrderFlow.Worker.Filters;

/// <summary>
/// Middleware global de pipeline do MassTransit.
/// Intercepta qualquer evento que implemente IIntegrationEvent e garante execução idempotente distribuída via Redis.
/// </summary>
public sealed class IdempotencyConsumeFilter<T> : IFilter<ConsumeContext<T>>
    where T : class, IIntegrationEvent
{
    private readonly IIdempotencyService _idempotencyService;
    private readonly ILogger<IdempotencyConsumeFilter<T>> _logger;
    private static readonly TimeSpan DefaultRetentionTtl = TimeSpan.FromHours(24);

    public IdempotencyConsumeFilter(
        IIdempotencyService idempotencyService,
        ILogger<IdempotencyConsumeFilter<T>> logger)
    {
        _idempotencyService = idempotencyService;
        _logger = logger;
    }

    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        var eventId = context.Message.EventId;
        var correlationId = context.Message.CorrelationId;

        var acquired = await _idempotencyService.TryAcquireProcessingLockAsync(
            eventId,
            DefaultRetentionTtl,
            context.CancellationToken);

        if (!acquired)
        {
            _logger.LogWarning(
                "[IdempotencyFilter] Evento duplicado ignorado | EventId: {EventId} | CorrelationId: {CorrelationId}",
                eventId, correlationId);
            return;
        }

        try
        {
            await next.Send(context);
        }
        catch
        {
            // Se falhar, libera o lock para permitir que as retentativas do MassTransit reprocessem o evento
            await _idempotencyService.ReleaseLockAsync(eventId, context.CancellationToken);
            throw;
        }
    }

    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope("redis-idempotency-filter");
    }
}