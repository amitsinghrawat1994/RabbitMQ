using Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderService.Sagas;
using OrderService;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// OpenTelemetry Configuration
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .AddSource(MassTransit.Logging.DiagnosticHeaders.DefaultListenerName) // MassTransit Tracing
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("order-service"))
            .AddAspNetCoreInstrumentation()
            .AddOtlpExporter(opts =>
            {
                opts.Endpoint = new Uri("http://localhost:4317");
            });
    });

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
            r.AddDbContext<DbContext, OrderDbContext>((provider, builder) =>
            {
                builder.UseSqlite(provider.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection"));
            });

            // For SQLite, avoid RowVersion optimistic concurrency for now; keep provider-specific lock statement provider
            r.SetOptimisticConcurrency(false);
            r.LockStatementProvider = new MassTransit.EntityFrameworkCoreIntegration.SqliteLockStatementProvider();

            // As an additional safety net, allow customizing the EF query if needed (keeps default behavior otherwise)
            r.CustomizeQuery(query => query);
        });

    // Register lightweight consumers to persist final order outcomes for audit/status lookup
    x.AddConsumer<OrderCompletedConsumer>();
    x.AddConsumer<OrderFailedConsumer>();

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

// Ensure DB is up-to-date with EF Migrations
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    // If the OrderState table already exists but EF Migrations history is empty, we need to perform a safe migration
    // path: 1) if OrderState table exists, add the OrderId column if missing and populate it; 2) insert a row into __EFMigrationsHistory marking the initial migration applied
    var conn = dbContext.Database.GetDbConnection();
    conn.Open();
    using (var cmd = conn.CreateCommand())
    {
        // Does the OrderState table exist?
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='OrderState'";
        var tableExists = Convert.ToInt32(cmd.ExecuteScalar() ?? 0) > 0;

        // Does migrations history table exist?
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory'";
        var migrationsTableExists = Convert.ToInt32(cmd.ExecuteScalar() ?? 0) > 0;

        var migrationsApplied = 0;
        if (migrationsTableExists)
        {
            cmd.CommandText = "SELECT COUNT(*) FROM __EFMigrationsHistory";
            migrationsApplied = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
        }

        if (tableExists && migrationsApplied == 0)
        {
            // Add OrderId column if missing
            cmd.CommandText = "PRAGMA table_info('OrderState');";
            using (var reader = cmd.ExecuteReader())
            {
                var hasOrderId = false;
                while (reader.Read())
                {
                    var colName = reader.GetString(1);
                    if (colName == "OrderId") { hasOrderId = true; break; }
                }
                reader.Close();

                if (!hasOrderId)
                {
                    cmd.CommandText = "ALTER TABLE OrderState ADD COLUMN OrderId TEXT;";
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = "UPDATE OrderState SET OrderId = CorrelationId;";
                    cmd.ExecuteNonQuery();
                }
            }

            // Ensure __EFMigrationsHistory exists
            if (!migrationsTableExists)
            {
                cmd.CommandText = "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (\"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, \"ProductVersion\" TEXT NOT NULL);";
                cmd.ExecuteNonQuery();
            }

            // Insert initial migration id so EF thinks it has been applied (use our migration id)
            cmd.CommandText = "INSERT OR IGNORE INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES('20260113000000_InitialCreate', '10.0.1')";
            cmd.ExecuteNonQuery();
        }
        else
        {
            // Safe to call Migrate normally when no pre-existing table or migrations are already applied
            dbContext.Database.Migrate();
        }
    }
    conn.Close();
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