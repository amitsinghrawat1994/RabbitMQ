using System;
using System.Linq;
using System.Threading.Tasks;
using Contracts;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Sagas;
using Xunit;

namespace ECommerce.Tests
{
    public class OrderSagaTests
    {
        [Fact]
        public async Task Should_Process_Order_Happy_Path()
        {
            await using var provider = new ServiceCollection()
                .AddMassTransitTestHarness(cfg =>
                {
                    cfg.AddSagaStateMachine<OrderStateMachine, OrderState>();
                })
                .BuildServiceProvider(true);

            var harness = provider.GetRequiredService<ITestHarness>();
            await harness.Start();

            var orderId = Guid.NewGuid();

            // 1. Submit Order
            await harness.Bus.Publish<OrderSubmitted>(new
            {
                OrderId = orderId,
                CustomerNumber = "TEST-001",
                TotalAmount = 100.00m
            });

            var sagaHarness = harness.GetSagaStateMachineHarness<OrderStateMachine, OrderState>();
            Assert.True(await sagaHarness.Consumed.Any<OrderSubmitted>());
            Assert.True(await sagaHarness.Created.Any(x => x.CorrelationId == orderId));
            Assert.NotNull(await sagaHarness.Exists(orderId, x => x.Submitted));

            // 2. Simulate Inventory Reserved
            await harness.Bus.Publish<StockReserved>(new { OrderId = orderId });
            
            Assert.True(await sagaHarness.Consumed.Any<StockReserved>());
            Assert.NotNull(await sagaHarness.Exists(orderId, x => x.InventoryReserved));
            Assert.True(await harness.Published.Any<ProcessPayment>());

            // 3. Simulate Payment Accepted
            await harness.Bus.Publish<PaymentAccepted>(new { OrderId = orderId });

            Assert.True(await sagaHarness.Consumed.Any<PaymentAccepted>());
            Assert.True(await harness.Published.Any<OrderCompleted>());
            
            // Finalized => removed from repo
            Assert.Null(await sagaHarness.Exists(orderId, x => x.Completed));
        }

        [Fact]
        public async Task Should_Fail_When_Stock_Shortage()
        {
            await using var provider = new ServiceCollection()
                .AddMassTransitTestHarness(cfg =>
                {
                    cfg.AddSagaStateMachine<OrderStateMachine, OrderState>();
                })
                .BuildServiceProvider(true);

            var harness = provider.GetRequiredService<ITestHarness>();
            await harness.Start();

            var orderId = Guid.NewGuid();

            // 1. Submit
            await harness.Bus.Publish<OrderSubmitted>(new { OrderId = orderId });
            var sagaHarness = harness.GetSagaStateMachineHarness<OrderStateMachine, OrderState>();
            Assert.NotNull(await sagaHarness.Exists(orderId, x => x.Submitted));

            // 2. Simulate Shortage
            await harness.Bus.Publish<StockShortage>(new 
            { 
                OrderId = orderId, 
                Reason = "Out of Stock" 
            });

            Assert.True(await sagaHarness.Consumed.Any<StockShortage>());
            // It should transition to Failed
            Assert.NotNull(await sagaHarness.Exists(orderId, x => x.Failed));
            
            // Should NOT publish ProcessPayment
            Assert.False(await harness.Published.Any<ProcessPayment>());
        }

        [Fact]
        public async Task Should_Fail_When_Payment_Rejected()
        {
            await using var provider = new ServiceCollection()
                .AddMassTransitTestHarness(cfg =>
                {
                    cfg.AddSagaStateMachine<OrderStateMachine, OrderState>();
                })
                .BuildServiceProvider(true);

            var harness = provider.GetRequiredService<ITestHarness>();
            await harness.Start();

            var orderId = Guid.NewGuid();

            // 1. Submit
            await harness.Bus.Publish<OrderSubmitted>(new { OrderId = orderId });
            var sagaHarness = harness.GetSagaStateMachineHarness<OrderStateMachine, OrderState>();

            // 2. Simulate Inventory Reserved
            await harness.Bus.Publish<StockReserved>(new { OrderId = orderId });
            Assert.NotNull(await sagaHarness.Exists(orderId, x => x.InventoryReserved));

            // 3. Simulate Payment Failed
            await harness.Bus.Publish<PaymentFailed>(new 
            { 
                OrderId = orderId, 
                Reason = "No Funds" 
            });

            Assert.True(await sagaHarness.Consumed.Any<PaymentFailed>());
            // It should transition to Failed
            Assert.NotNull(await sagaHarness.Exists(orderId, x => x.Failed));
            
            // Should NOT publish OrderCompleted
            Assert.False(await harness.Published.Any<OrderCompleted>());
        }
    }
}