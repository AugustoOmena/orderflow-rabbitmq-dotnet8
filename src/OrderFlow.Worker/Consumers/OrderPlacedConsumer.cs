using MassTransit;
using Microsoft.Extensions.Logging;
using OrderFlow.Contracts.Events;
using OrderFlow.Worker.Services;

namespace OrderFlow.Worker.Consumers;

public sealed class OrderPlacedConsumer : IConsumer<OrderPlacedEvent>
{
    private readonly IIdempotencyService _idempotencyService;
    private readonly ILogger<OrderPlacedConsumer> _logger;

    public OrderPlacedConsumer(
        IIdempotencyService idempotencyService,
        ILogger<OrderPlacedConsumer> logger)
    {
        _idempotencyService = idempotencyService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderPlacedEvent> context)
    {
        var message = context.Message;
        var correlationId = message.CorrelationId;
        var retryAttempt = context.GetRetryAttempt();

        _logger.LogInformation(
            "--> Iniciando processamento | OrderId: {OrderId} | CorrelationId: {CorrelationId} | Tentativa: {RetryAttempt}",
            message.OrderId, correlationId, retryAttempt);

        // 1. Verificação de Idempotência (Garantia Exactly-Once lógica sobre entrega At-Least-Once)
        if (await _idempotencyService.HasBeenProcessedAsync(correlationId, context.CancellationToken))
        {
            _logger.LogWarning(
                "Mensagem duplicada detectada (CorrelationId: {CorrelationId}). Ignorando reprocessamento e confirmando ACK.",
                correlationId);
            return;
        }

        // 2. Simulação controlada de falha a jusante (ex: Gateway de Pagamento fora do ar)
        if (message.SimulateDownstreamFailure)
        {
            _logger.LogError(
                "Falha crítica simulada ao integrar pedido {OrderId} (Tentativa {RetryAttempt}). Disparando exceção para motor de retry...",
                message.OrderId, retryAttempt);

            throw new InvalidOperationException(
                $"Downstream payment gateway unavailable for OrderId: {message.OrderId}");
        }

        // 3. Simulação de processamento de negócio (Reserva de estoque + Liquidação)
        await Task.Delay(300, context.CancellationToken);

        // 4. Registra chave de idempotência após sucesso
        await _idempotencyService.MarkAsProcessedAsync(correlationId, context.CancellationToken);

        _logger.LogInformation(
            "==> Pedido {OrderId} liquidado com sucesso! Valor: {TotalAmount:C} | Cliente: {CustomerEmail}",
            message.OrderId, message.TotalAmount, message.CustomerEmail);
    }
}