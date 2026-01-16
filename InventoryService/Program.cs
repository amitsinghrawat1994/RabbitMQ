using System;
using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using InventoryService;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;

var builder = Host.CreateApplicationBuilder(args);

// OpenTelemetry Configuration
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .AddSource(MassTransit.Logging.DiagnosticHeaders.DefaultListenerName)
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("inventory-service"))
            .AddOtlpExporter(opts =>
            {
                opts.Endpoint = new Uri("http://localhost:4317");
            });
    });

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<CheckInventoryConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitConfig = context.GetRequiredService<IConfiguration>().GetSection("RabbitMQ");
        cfg.Host(rabbitConfig["Host"] ?? "localhost", "/", h =>
        {
            h.Username(rabbitConfig["Username"] ?? "guest");
            h.Password(rabbitConfig["Password"] ?? "guest");
        });

        cfg.ConfigureEndpoints(context);
    });
});

var host = builder.Build();
host.Run();