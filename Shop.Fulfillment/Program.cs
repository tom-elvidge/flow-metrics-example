using System.Collections.Generic;
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
            .AddSource("Fulfillment")
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("fulfillment"))
            .AddOtlpExporter();
    });

builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
    ConnectionMultiplexer.Connect("redis:6379"));

var app = builder.Build();
var activitySource = new ActivitySource("Fulfillment");

var redis = app.Services.GetRequiredService<IConnectionMultiplexer>();
var subscriber = redis.GetSubscriber();

// Success path
await subscriber.SubscribeAsync("fulfillment-requests", async (channel, message) =>
{
    using var activity = activitySource.StartActivity("fulfill-order");
    
    var correlationId = message.ToString();
    
    // Simulate fulfillment
    await Task.Delay(150);
    
    // Mark flow boundary end - SUCCESS
    const string flowName = "order_processing";
    activity?.AddEvent(new ActivityEvent($"flow.{flowName}.end", tags: new ActivityTagsCollection([
        new KeyValuePair<string, object>("correlation-id", correlationId),
        new KeyValuePair<string, object>("outcome", "success")
    ])));
});

// Failure path  
await subscriber.SubscribeAsync("order-failures", async (channel, message) =>
{
    using var activity = activitySource.StartActivity("handle-order-failure");
    
    var parts = message.ToString().Split('|');
    var correlationId = parts[0];
    var reason = parts[1];
    
    await Task.Delay(50);
    
    // Mark flow boundary end - SUCCESS
    const string flowName = "order_processing";
    activity?.AddEvent(new ActivityEvent($"flow.{flowName}.end", tags: new ActivityTagsCollection([
        new KeyValuePair<string, object>("correlation-id", correlationId),
        new KeyValuePair<string, object>("outcome", "failure")
    ])));
});

app.Run();