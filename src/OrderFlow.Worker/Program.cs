using OrderFlow.Worker.Extensions;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWorkerInfrastructure(builder.Configuration);

var host = builder.Build();
await host.RunAsync();