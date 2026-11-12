using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json;

namespace OrderFlow.Api.Extensions;

public static class MessagingExtensions
{
    public static IServiceCollection AddMessagingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var rabbitHost = configuration["RabbitMq:Host"] ?? "localhost";
        var rabbitUser = configuration["RabbitMq:User"] ?? "guest";
        var rabbitPass = configuration["RabbitMq:Pass"] ?? "guest";

        services.AddMassTransit(x =>
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

        services.AddHealthChecks()
            .AddRabbitMQ(
                rabbitConnectionString: $"amqp://{rabbitUser}:{rabbitPass}@{rabbitHost}:5672/",
                name: "rabbitmq_broker");

        return services;
    }

    public static WebApplication MapStructuredHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                var payload = JsonSerializer.Serialize(new
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
                await context.Response.WriteAsync(payload);
            }
        });

        return app;
    }
}