using StackExchange.Redis;

namespace OrderFlow.Worker.Services;

public interface IIdempotencyService
{
    /// <summary>
    /// Tenta adquirir o lock/registro atômico de processamento para o EventId.
    /// Retorna true se é a primeira vez que o evento está sendo processado.
    /// </summary>
    Task<bool> TryAcquireProcessingLockAsync(Guid eventId, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Libera a chave em caso de falha transitória para permitir que o mecanismo de Retry reexecute o evento.
    /// </summary>
    Task ReleaseLockAsync(Guid eventId, CancellationToken cancellationToken = default);
}

public sealed class RedisIdempotencyService : IIdempotencyService
{
    private readonly IDatabase _database;
    private const string KeyPrefix = "orderflow:idempotency:";

    public RedisIdempotencyService(IConnectionMultiplexer redis)
    {
        _database = redis.GetDatabase();
    }

    public async Task<bool> TryAcquireProcessingLockAsync(Guid eventId, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var key = $"{KeyPrefix}{eventId}";
        // Operação atômica SET NX EX no Redis: evita race condition entre múltiplos workers concorrentes
        return await _database.StringSetAsync(key, DateTime.UtcNow.ToString("O"), ttl, When.NotExists);
    }

    public async Task ReleaseLockAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var key = $"{KeyPrefix}{eventId}";
        await _database.KeyDeleteAsync(key);
    }
}