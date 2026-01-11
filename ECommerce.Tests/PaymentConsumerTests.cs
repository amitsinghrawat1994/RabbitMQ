using System;
using System.Linq;
using System.Threading.Tasks;
using Contracts;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using PaymentService;
using Xunit;

namespace ECommerce.Tests
{
    public class PaymentConsumerTests
    {
        [Fact]
        public async Task Should_Accept_Payment_When_Valid()
        {
            await using var provider = new ServiceCollection()
                .AddMassTransitTestHarness(cfg =>
                {
                    cfg.AddConsumer<ProcessPaymentConsumer>();
                })
                .BuildServiceProvider(true);

            var harness = provider.GetRequiredService<ITestHarness>();
            await harness.Start();

            // Valid GUID (ends in not 1 or 2)
            var orderId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa3");

            await harness.Bus.Publish<ProcessPayment>(new
            {
                OrderId = orderId,
                Amount = 100.00m
            });

            Assert.True(await harness.Consumed.Any<ProcessPayment>());
            Assert.True(await harness.Published.Any<PaymentAccepted>());
            Assert.False(await harness.Published.Any<PaymentFailed>());
        }

        [Fact]
        public async Task Should_Fail_Payment_When_Guid_Ends_In_1()
        {
            await using var provider = new ServiceCollection()
                .AddMassTransitTestHarness(cfg =>
                {
                    cfg.AddConsumer<ProcessPaymentConsumer>();
                })
                .BuildServiceProvider(true);

            var harness = provider.GetRequiredService<ITestHarness>();
            await harness.Start();

            // Invalid GUID (ends in 1)
            var orderId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa1");

            await harness.Bus.Publish<ProcessPayment>(new
            {
                OrderId = orderId,
                Amount = 100.00m
            });

            Assert.True(await harness.Consumed.Any<ProcessPayment>());
            Assert.True(await harness.Published.Any<PaymentFailed>());
            Assert.False(await harness.Published.Any<PaymentAccepted>());
        }

        [Fact]
        public async Task Should_Throw_Exception_When_Guid_Ends_In_2()
        {
            await using var provider = new ServiceCollection()
                .AddMassTransitTestHarness(cfg =>
                {
                    cfg.AddConsumer<ProcessPaymentConsumer>();
                })
                .BuildServiceProvider(true);

            var harness = provider.GetRequiredService<ITestHarness>();
            await harness.Start();

            // Error GUID (ends in 2)
            var orderId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa2");

            await harness.Bus.Publish<ProcessPayment>(new
            {
                OrderId = orderId,
                Amount = 100.00m
            });

            // The consumer should throw, which means it consumed but faulted.
            Assert.True(await harness.Consumed.Any<ProcessPayment>());
            
            // In the harness, faulted messages are tracked
            // We verify synchronously since we already awaited the consumption above
            var consumedMessages = harness.Consumed.Select<ProcessPayment>().ToList();
            Assert.Contains(consumedMessages, x => x.Context.Message.OrderId == orderId && x.Exception != null);
            
            Assert.False(await harness.Published.Any<PaymentAccepted>());
            Assert.False(await harness.Published.Any<PaymentFailed>());
        }
    }
}
