using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .AddSource("Payments")
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("payments"))
            .AddOtlpExporter();
    });

builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
    ConnectionMultiplexer.Connect("redis:6379"));

var app = builder.Build();
var activitySource = new ActivitySource("Payments");

// Redis subscriber
var redis = app.Services.GetRequiredService<IConnectionMultiplexer>();
var subscriber = redis.GetSubscriber();

await subscriber.SubscribeAsync("payment-requests", async (channel, message) =>
{
    using var activity = activitySource.StartActivity("process-payment");
    
    var parts = message.ToString().Split('|');
    var correlationId = parts[0];
    var amount = decimal.Parse(parts[1]);
    
    // Simulate payment processing
    await Task.Delay(200);
    
    var success = Random.Shared.Next(100) < 90; // 90% success rate
    
    var db = redis.GetDatabase();
    if (success)
    {
        await db.PublishAsync("fulfillment-requests", correlationId);
    }
    else
    {
        await db.PublishAsync("order-failures", $"{correlationId}|payment-failed");
    }
});

app.Run();