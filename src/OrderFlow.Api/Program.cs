using OrderFlow.Api.Extensions;
using OrderFlow.Api.Features.Orders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMessagingInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapStructuredHealthChecks();
app.MapCreateOrderEndpoint();

app.Run();