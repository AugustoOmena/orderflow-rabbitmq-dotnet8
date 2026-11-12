using MassTransit;
using OrderFlow.Contracts.Events;

namespace OrderFlow.Worker.Consumers;

public sealed class OrderPlacedConsumer : IConsumer<OrderPlacedEvent>
{
    private readonly ILogger<OrderPlacedConsumer> _logger;

    public OrderPlacedConsumer(ILogger<OrderPlacedConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderPlacedEvent> context)
    {
        var message = context.Message;
        var retryAttempt = context.GetRetryAttempt();

        _logger.LogInformation(
            "--> Processando pedido | OrderId: {OrderId} | CorrelationId: {CorrelationId} | Itens: {ItemCount} | Tentativa: {RetryAttempt}",
            message.OrderId, message.CorrelationId, message.Items.Count, retryAttempt);

        if (message.SimulateDownstreamFailure)
        {
            _logger.LogError(
                "Falha simulada no gateway de pagamento para OrderId: {OrderId} (Tentativa {RetryAttempt}).",
                message.OrderId, retryAttempt);

            throw new InvalidOperationException(
                $"Downstream payment gateway unavailable for OrderId: {message.OrderId}");
        }

        // Simulação de reserva de estoque e captura financeira
        await Task.Delay(250, context.CancellationToken);

        _logger.LogInformation(
            "==> Pedido {OrderId} liquidado com sucesso | Total: {TotalAmount} {Currency} | Cliente: {CustomerEmail}",
            message.OrderId, message.TotalAmount, message.Currency, message.CustomerEmail);
    }
}