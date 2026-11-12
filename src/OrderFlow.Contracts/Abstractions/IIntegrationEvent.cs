namespace OrderFlow.Contracts.Abstractions;

/// <summary>
/// Contrato base obrigatório para todos os eventos de integração do ecossistema.
/// Garante rastreabilidade distribuída e unicidade para idempotência.
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    Guid CorrelationId { get; }
    DateTime OccurredOnUtc { get; }
}