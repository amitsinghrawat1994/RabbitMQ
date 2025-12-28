using System;
using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using PaymentService;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ProcessPaymentConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitConfig = context.GetRequiredService<IConfiguration>().GetSection("RabbitMQ");
        cfg.Host(rabbitConfig["Host"] ?? "localhost", "/", h =>
        {
            h.Username(rabbitConfig["Username"] ?? "guest");
            h.Password(rabbitConfig["Password"] ?? "guest");
        });

        // Retry Policy Configuration
        cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromMilliseconds(500)));

        cfg.ConfigureEndpoints(context);
    });
});

var host = builder.Build();
host.Run();