using System;
using System.Linq;
using System.Threading.Tasks;
using Contracts;
using InventoryService;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ECommerce.Tests
{
    public class InventoryConsumerTests
    {
        [Fact]
        public async Task Should_Reserve_Stock_When_Available()
        {
            await using var provider = new ServiceCollection()
                .AddMassTransitTestHarness(cfg =>
                {
                    cfg.AddConsumer<CheckInventoryConsumer>();
                })
                .BuildServiceProvider(true);

            var harness = provider.GetRequiredService<ITestHarness>();
            await harness.Start();

            // Use a GUID that does NOT end in '0'
            var orderId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa3");

            await harness.Bus.Publish<CheckInventory>(new
            {
                OrderId = orderId,
                Items = new[] { new { ProductId = "123", Quantity = 1 } }
            });

            Assert.True(await harness.Consumed.Any<CheckInventory>());
            Assert.True(await harness.Published.Any<StockReserved>());
            Assert.False(await harness.Published.Any<StockShortage>());
        }

        [Fact]
        public async Task Should_Report_Shortage_When_Guid_Ends_In_0()
        {
            await using var provider = new ServiceCollection()
                .AddMassTransitTestHarness(cfg =>
                {
                    cfg.AddConsumer<CheckInventoryConsumer>();
                })
                .BuildServiceProvider(true);

            var harness = provider.GetRequiredService<ITestHarness>();
            await harness.Start();

            // Use a GUID that ends in '0'
            var orderId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa0");

            await harness.Bus.Publish<CheckInventory>(new
            {
                OrderId = orderId
            });

            Assert.True(await harness.Consumed.Any<CheckInventory>());
            Assert.True(await harness.Published.Any<StockShortage>());
            Assert.False(await harness.Published.Any<StockReserved>());
        }
    }
}
