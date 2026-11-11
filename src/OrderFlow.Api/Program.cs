using System.Text.Json;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OrderFlow.Contracts.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var rabbitHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
var rabbitUser = builder.Configuration["RabbitMq:User"] ?? "guest";
var rabbitPass = builder.Configuration["RabbitMq:Pass"] ?? "guest";

// Configuração do MassTransit como Publisher
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitHost, "/", h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });

        cfg.ConfigureEndpoints(context);
    });
});

// Health Check ativo validando conexão AMQP com o RabbitMQ
builder.Services.AddHealthChecks()
    .AddRabbitMQ(
        rabbitConnectionString: $"amqp://{rabbitUser}:{rabbitPass}@{rabbitHost}:5672/",
        name: "rabbitmq_broker");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Endpoint de Health Check estruturado em JSON
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            timestampUtc = DateTime.UtcNow,
            checks = report.Entries.Select(e => new
            {
                component = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description
            })
        });
        await context.Response.WriteAsync(response);
    }
});

// Endpoint de ingestão de pedidos
app.MapPost("/api/orders", async (
    CreateOrderRequest request,
    IPublishEndpoint publishEndpoint,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.CustomerEmail) || request.TotalAmount <= 0)
    {
        return Results.BadRequest(new { Error = "Email válido e valor maior que zero são obrigatórios." });
    }

    var correlationId = Guid.NewGuid();
    var orderId = Guid.NewGuid();

    logger.LogInformation(
        "Recebendo requisição de pedido. OrderId: {OrderId} | CorrelationId: {CorrelationId} | Valor: {TotalAmount}",
        orderId, correlationId, request.TotalAmount);

    var integrationEvent = new OrderPlacedEvent
    {
        CorrelationId = correlationId,
        OrderId = orderId,
        CustomerEmail = request.CustomerEmail,
        TotalAmount = request.TotalAmount,
        CreatedAtUtc = DateTime.UtcNow,
        SimulateDownstreamFailure = request.SimulateDownstreamFailure
    };

    // Publicação persistente com CorrelationId propagado nos headers do broker
    await publishEndpoint.Publish(integrationEvent, ctx =>
    {
        ctx.CorrelationId = correlationId;
        ctx.Durable = true;
    }, cancellationToken);

    logger.LogInformation(
        "Evento OrderPlacedEvent publicado com sucesso no broker. OrderId: {OrderId} | CorrelationId: {CorrelationId}",
        orderId, correlationId);

    return Results.Accepted($"/api/orders/{orderId}", new
    {
        OrderId = orderId,
        CorrelationId = correlationId,
        Status = "Enqueued"
    });
})
.WithName("CreateOrder");

app.Run();

public record CreateOrderRequest(
    string CustomerEmail,
    decimal TotalAmount,
    bool SimulateDownstreamFailure = false);