using Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderService.Sagas;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// EF Core Configuration
builder.Services.AddDbContext<OrderDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// MassTransit Configuration
builder.Services.AddMassTransit(x =>
{
    // Register Saga State Machine
    x.AddSagaStateMachine<OrderStateMachine, OrderState>()
        .EntityFrameworkRepository(r =>
        {
            r.ConcurrencyMode = ConcurrencyMode.Optimistic; // or Pessimistic
            
            r.AddDbContext<DbContext, OrderDbContext>((provider,builder) =>
            {
                builder.UseSqlite(provider.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection"));
            });
        });

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitConfig = builder.Configuration.GetSection("RabbitMQ");
        cfg.Host(rabbitConfig["Host"] ?? "localhost", "/", h =>
        {
            h.Username(rabbitConfig["Username"] ?? "guest");
            h.Password(rabbitConfig["Password"] ?? "guest");
        });

        // Configure endpoints for Sagas
        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();

// Ensure DB is created
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    dbContext.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();