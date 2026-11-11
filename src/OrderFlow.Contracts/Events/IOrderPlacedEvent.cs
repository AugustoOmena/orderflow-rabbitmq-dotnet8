namespace OrderFlow.Contracts.Events;

/// <summary>
/// Evento emitido quando um novo pedido é registrado na plataforma.
/// Contrato imutável compartilhado entre produtores e consumidores.
/// </summary>
public record OrderPlacedEvent
{
    /// <summary>
    /// Identificador único para tracing distribuído e garantia de idempotência.
    /// </summary>
    public Guid CorrelationId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Identificador do pedido de negócio.
    /// </summary>
    public Guid OrderId { get; init; }

    /// <summary>
    /// Email do cliente associado ao pedido.
    /// </summary>
    public string CustomerEmail { get; init; } = string.Empty;

    /// <summary>
    /// Valor monetário total da transação.
    /// </summary>
    public decimal TotalAmount { get; init; }

    /// <summary>
    /// Timestamp UTC do momento da emissão do evento.
    /// </summary>
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Flag de controle usada para simular falhas a jusante em cenários de teste de resiliência/DLQ.
    /// </summary>
    public bool SimulateDownstreamFailure { get; init; }
}