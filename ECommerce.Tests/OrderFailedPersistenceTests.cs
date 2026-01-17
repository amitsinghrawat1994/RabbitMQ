using System;
using System.IO;
using System.Threading.Tasks;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Sagas;
using Xunit;
using Contracts;

namespace ECommerce.Tests
{
    public class OrderFailedPersistenceTests : IDisposable
    {
        private const string DbPath = "orders_failed_test.db";

        public OrderFailedPersistenceTests()
        {
            if (File.Exists(DbPath)) File.Delete(DbPath);
        }

        [Fact]
        public async Task OrderFailed_Is_Persisted()
        {
            var services = new ServiceCollection();

            services.AddDbContext<OrderDbContext>(options =>
            {
                options.UseSqlite($"Data Source={DbPath}");
            });

            services.AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<OrderService.OrderFailedConsumer>();
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

            var orderId = Guid.NewGuid().ToString();

            await harness.Bus.Publish<OrderFailed>(new { OrderId = orderId, Reason = "Test failure" });

            // Give consumer a moment to process
            await Task.Delay(500);

            using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={DbPath}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM Orders WHERE OrderId = @id AND Status = 'Failed'";
                var p = cmd.CreateParameter();
                p.ParameterName = "@id";
                p.Value = orderId;
                cmd.Parameters.Add(p);
                var count = Convert.ToInt32(cmd.ExecuteScalar());
                Assert.Equal(1, count);

                cmd.CommandText = "SELECT Reason FROM Orders WHERE OrderId = @id";
                using var reader = cmd.ExecuteReader();
                reader.Read();
                var reason = reader.GetString(0);
                Assert.Equal("Test failure", reason);
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
                // Ignore
            }
        }
    }
}
