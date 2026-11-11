using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrderFlow.Worker.Consumers;
using OrderFlow.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<IIdempotencyService, MemoryIdempotencyService>();

var rabbitHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
var rabbitUser = builder.Configuration["RabbitMq:User"] ?? "guest";
var rabbitPass = builder.Configuration["RabbitMq:Pass"] ?? "guest";

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderPlacedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitHost, "/", h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });

        // Configuração explícita do Endpoint (Fila + Resiliência + QoS)
        cfg.ReceiveEndpoint("order-flow.order-placed", e =>
        {
            // Durabilidade da fila no broker
            e.Durable = true;

            // Controle de vazão (QoS): máximo de 16 mensagens não confirmadas em trânsito por consumidor
            e.PrefetchCount = 16;

            // Estratégia de Retry: 3 tentativas adicionais com backoff incremental (1s, 3s, 5s)
            // Esgotadas as tentativas, a mensagem é movida para 'order-flow.order-placed_error' (DLQ)
            e.UseMessageRetry(r =>
            {
                r.Incremental(retryLimit: 3, initialInterval: TimeSpan.FromSeconds(1), intervalIncrement: TimeSpan.FromSeconds(2));
                r.Ignore<ArgumentException>();
            });

            e.ConfigureConsumer<OrderPlacedConsumer>(context);
        });
    });
});

var host = builder.Build();
await host.RunAsync();