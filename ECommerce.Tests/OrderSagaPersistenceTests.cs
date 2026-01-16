using System;
using System.IO;
using System.Threading.Tasks;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Sagas;
using Contracts;
using Xunit;

namespace ECommerce.Tests
{
    public class OrderSagaPersistenceTests : IDisposable
    {
        private const string DbPath = "orders_test.db";

        public OrderSagaPersistenceTests()
        {
            if (File.Exists(DbPath)) File.Delete(DbPath);
        }

        [Fact]
        public async Task Saga_Is_Persisted_To_Sqlite()
        {
            var services = new ServiceCollection();

            services.AddDbContext<OrderDbContext>(options =>
            {
                options.UseSqlite($"Data Source={DbPath}");
            });

            services.AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<OrderStateMachine, OrderState>()
                    .EntityFrameworkRepository(r =>
                    {
                        r.AddDbContext<DbContext, OrderDbContext>((provider, builder) =>
                        {
                            builder.UseSqlite($"Data Source={DbPath}");
                        });

                        r.SetOptimisticConcurrency(true);
                        r.LockStatementProvider = new MassTransit.EntityFrameworkCoreIntegration.SqliteLockStatementProvider();
                        r.CustomizeQuery(query => query);
                    });
            });

            await using var provider = services.BuildServiceProvider(true);
            var harness = provider.GetRequiredService<ITestHarness>();
            await harness.Start();

            // Ensure DB created
            using (var scope = provider.CreateScope())
            {
                var ctx = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
                ctx.Database.EnsureCreated();
            }

            // Use an order id whose string representation ends with '0' so Inventory will publish StockShortage
            var orderId = new Guid("00000000-0000-0000-0000-000000000010");

            await harness.Bus.Publish<OrderSubmitted>(new
            {
                OrderId = orderId.ToString(),
                Timestamp = DateTime.UtcNow,
                CustomerNumber = "PERSIST-001",
                TotalAmount = 10.0m
            });

            var sagaHarness = harness.GetSagaStateMachineHarness<OrderStateMachine, OrderState>();

            // The saga instance should consume the OrderSubmitted message
            Assert.True(await sagaHarness.Consumed.Any<OrderSubmitted>());

            // Simulate a StockShortage for this specific saga (deterministic)
            await harness.Bus.Publish<StockShortage>(new { OrderId = orderId.ToString(), Reason = "Out of stock" });
            Assert.True(await sagaHarness.Consumed.Any<StockShortage>());
            // Verify the saga created an entry with the provided OrderId
            Assert.True(await sagaHarness.Created.Any(x => x.OrderId == orderId.ToString()));

            await harness.Stop();

            // Dispose provider early so EF releases the DB file lock before we inspect it
            await provider.DisposeAsync();

            // Inspect DB file directly using a fresh SqliteConnection to avoid EF locks
            using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={DbPath}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM OrderState;";
                var count = Convert.ToInt32(cmd.ExecuteScalar());
                Assert.True(count >= 1, "Expected at least one saga row in the SQLite DB");

                // Query the persisted row and check CustomerNumber
                cmd.CommandText = "SELECT CustomerNumber FROM OrderState WHERE OrderId = @id";
                var p = cmd.CreateParameter();
                p.ParameterName = "@id";
                p.Value = orderId.ToString();
                cmd.Parameters.Add(p);
                var customer = cmd.ExecuteScalar() as string;
                Assert.Equal("PERSIST-001", customer);
            }
        }

        public void Dispose()
        {
            try
            {
                if (File.Exists(DbPath)) File.Delete(DbPath);
            }
            catch (IOException)
            {
                // Ignore file-in-use errors during test cleanup
            }
        }
    }
}
