using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .AddSource("Orders")
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("orders"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter();
    });

builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
    ConnectionMultiplexer.Connect("redis:6379"));

var app = builder.Build();

var activitySource = new ActivitySource("Orders");

app.MapPost("/orders", async (OrderRequest request, IConnectionMultiplexer redis) =>
{
    using var activity = activitySource.StartActivity("create-order");
    
    var orderId = Guid.NewGuid().ToString();
    var correlationId = $"order-{orderId}";
    
    // Mark flow boundary start
    const string flowName = "order_processing";
    activity?.AddEvent(new ActivityEvent($"flow.{flowName}.start", tags: new ActivityTagsCollection([
        new KeyValuePair<string, object>("correlation-id", correlationId)
    ])));
    
    await Task.Delay(100);
    
    var db = redis.GetDatabase();
    await db.PublishAsync("payment-requests", $"{correlationId}|{request.Amount}");
    
    return Results.Ok(new { OrderId = orderId, Status = "Processing" });
});

app.Run();

public record OrderRequest(decimal Amount, string ProductId);