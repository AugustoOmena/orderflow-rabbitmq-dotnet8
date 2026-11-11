using System.Collections.Concurrent;

namespace OrderFlow.Worker.Services;

public interface IIdempotencyService
{
    Task<bool> HasBeenProcessedAsync(Guid correlationId, CancellationToken cancellationToken = default);
    Task MarkAsProcessedAsync(Guid correlationId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementação thread-safe em memória para controle de idempotência.
/// Em produção distribuída com múltiplas réplicas, substituir por Redis (SETNX) ou tabela com Unique Constraint.
/// </summary>
public sealed class MemoryIdempotencyService : IIdempotencyService
{
    private readonly ConcurrentDictionary<Guid, DateTime> _processedMessages = new();

    public Task<bool> HasBeenProcessedAsync(Guid correlationId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_processedMessages.ContainsKey(correlationId));
    }

    public Task MarkAsProcessedAsync(Guid correlationId, CancellationToken cancellationToken = default)
    {
        _processedMessages.TryAdd(correlationId, DateTime.UtcNow);
        return Task.CompletedTask;
    }
}