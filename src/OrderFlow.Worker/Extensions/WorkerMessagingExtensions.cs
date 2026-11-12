using MassTransit;
using OrderFlow.Worker.Consumers;
using OrderFlow.Worker.Filters;
using OrderFlow.Worker.Services;
using StackExchange.Redis;

namespace OrderFlow.Worker.Extensions;

public static class WorkerMessagingExtensions
{
    public static IServiceCollection AddWorkerInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var redisConnectionString = configuration["Redis:ConnectionString"] ?? "localhost:6379";
        var rabbitHost = configuration["RabbitMq:Host"] ?? "localhost";
        var rabbitUser = configuration["RabbitMq:User"] ?? "guest";
        var rabbitPass = configuration["RabbitMq:Pass"] ?? "guest";

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnectionString));

        services.AddSingleton<IIdempotencyService, RedisIdempotencyService>();

        services.AddMassTransit(x =>
        {
            x.AddConsumer<OrderPlacedConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbitHost, "/", h =>
                {
                    h.Username(rabbitUser);
                    h.Password(rabbitPass);
                });

                // Registra o Filtro de Idempotência no pipeline global de consumo
                cfg.UseConsumeFilter(typeof(IdempotencyConsumeFilter<>), context);

                cfg.ReceiveEndpoint("order-flow.order-placed", e =>
                {
                    e.Durable = true;
                    e.PrefetchCount = 16;

                    e.UseMessageRetry(r =>
                    {
                        r.Incremental(
                            retryLimit: 3,
                            initialInterval: TimeSpan.FromSeconds(1),
                            intervalIncrement: TimeSpan.FromSeconds(2));
                        r.Ignore<ArgumentException>();
                    });

                    e.ConfigureConsumer<OrderPlacedConsumer>(context);
                });
            });
        });

        return services;
    }
}