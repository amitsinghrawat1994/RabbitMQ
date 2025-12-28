using System;
using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using InventoryService;

var builder = Host.CreateApplicationBuilder(args);

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