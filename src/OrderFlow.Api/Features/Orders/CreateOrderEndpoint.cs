using System.ComponentModel.DataAnnotations;
using MassTransit;
using OrderFlow.Contracts.Events;

namespace OrderFlow.Api.Features.Orders;

public static class CreateOrderEndpoint
{
    public static IEndpointRouteBuilder MapCreateOrderEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/orders", HandleAsync)
           .WithName("CreateOrder")
           .Produces<CreateOrderResponse>(StatusCodes.Status202Accepted)
           .ProducesValidationProblem();

        return app;
    }

    private static async Task<IResult> HandleAsync(
        CreateOrderRequest request,
        IPublishEndpoint publishEndpoint,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("CreateOrderEndpoint");

        var validationErrors = ValidateRequest(request);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var correlationId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var totalAmount = request.Items.Sum(i => i.Quantity * i.UnitPrice);

        var integrationEvent = new OrderPlacedEvent
        {
            CorrelationId = correlationId,
            OrderId = orderId,
            CustomerEmail = request.CustomerEmail,
            Currency = request.Currency,
            TotalAmount = totalAmount,
            Items = request.Items
                .Select(i => new OrderItemDto(i.Sku, i.ProductName, i.Quantity, i.UnitPrice))
                .ToArray(),
            SimulateDownstreamFailure = request.SimulateDownstreamFailure
        };

        await publishEndpoint.Publish(integrationEvent, ctx =>
        {
            ctx.MessageId = integrationEvent.EventId;
            ctx.CorrelationId = correlationId;
            ctx.Durable = true;
        }, cancellationToken);

        logger.LogInformation(
            "Evento OrderPlacedEvent publicado | EventId: {EventId} | OrderId: {OrderId} | CorrelationId: {CorrelationId} | Total: {TotalAmount} {Currency}",
            integrationEvent.EventId, orderId, correlationId, totalAmount, request.Currency);

        return Results.Accepted(
            $"/api/orders/{orderId}",
            new CreateOrderResponse(orderId, correlationId, integrationEvent.EventId, totalAmount, "Enqueued"));
    }

    private static Dictionary<string, string[]> ValidateRequest(CreateOrderRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.CustomerEmail) || !new EmailAddressAttribute().IsValid(request.CustomerEmail))
            errors["customerEmail"] = ["Um endereço de e-mail válido é obrigatório."];

        if (request.Items is null || request.Items.Count == 0)
            errors["items"] = ["O pedido deve conter ao menos um item."];
        else if (request.Items.Any(i => i.Quantity <= 0 || i.UnitPrice <= 0 || string.IsNullOrWhiteSpace(i.Sku)))
            errors["items"] = ["Todos os itens devem possuir SKU, quantidade > 0 e preço unitário > 0."];

        return errors;
    }
}

public sealed record CreateOrderRequest(
    string CustomerEmail,
    List<CreateOrderItemRequest> Items,
    string Currency = "BRL",
    bool SimulateDownstreamFailure = false);

public sealed record CreateOrderItemRequest(
    string Sku,
    string ProductName,
    int Quantity,
    decimal UnitPrice);

public sealed record CreateOrderResponse(
    Guid OrderId,
    Guid CorrelationId,
    Guid EventId,
    decimal TotalAmount,
    string Status);