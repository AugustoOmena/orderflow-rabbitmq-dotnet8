using OrderFlow.Contracts.Abstractions;

namespace OrderFlow.Contracts.Events;

public sealed record OrderPlacedEvent : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;

    public Guid OrderId { get; init; }
    public string CustomerEmail { get; init; } = string.Empty;
    public string Currency { get; init; } = "BRL";
    public decimal TotalAmount { get; init; }
    public IReadOnlyCollection<OrderItemDto> Items { get; init; } = Array.Empty<OrderItemDto>();
    public bool SimulateDownstreamFailure { get; init; }
}

public sealed record OrderItemDto(
    string Sku,
    string ProductName,
    int Quantity,
    decimal UnitPrice);