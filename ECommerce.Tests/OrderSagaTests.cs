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
        public async Task Should_Create_Saga_And_Process_Order_Flow()
        {
            // Arrange
            await using var provider = new ServiceCollection()
                .AddMassTransitTestHarness(cfg =>
                {
                    cfg.AddSagaStateMachine<OrderStateMachine, OrderState>();
                })
                .BuildServiceProvider(true);

            var harness = provider.GetRequiredService<ITestHarness>();

            await harness.Start();

            var orderId = Guid.NewGuid();

            // Act 1: Submit Order
            await harness.Bus.Publish<OrderSubmitted>(new
            {
                OrderId = orderId,
                Timestamp = DateTime.UtcNow,
                CustomerNumber = "TEST-001",
                TotalAmount = 100.00m
            });

            // Assert 1: Saga Created
            Assert.True(await harness.Consumed.Any<OrderSubmitted>());
            var sagaHarness = harness.GetSagaStateMachineHarness<OrderStateMachine, OrderState>();
            
            Assert.True(await sagaHarness.Consumed.Any<OrderSubmitted>());
            Assert.True(await sagaHarness.Created.Any(x => x.CorrelationId == orderId));

            var instance = sagaHarness.Created.Contains(orderId);
            Assert.NotNull(instance);
            
            // Check State: Submitted
            // (Note: InMemory repository might need strict timing or checking the instance via `Sagas` collection if exposed, 
            // but checking the state via `Exists` method on saga harness is standard)
            Assert.NotNull(await sagaHarness.Exists(orderId, x => x.Submitted));

            // Check Command Sent: CheckInventory
            Assert.True(await harness.Published.Any<CheckInventory>());

            // Act 2: Simulate Inventory Reserved
            await harness.Bus.Publish<StockReserved>(new
            {
                OrderId = orderId
            });

            // Assert 2: Saga Transitions to InventoryReserved
            Assert.True(await sagaHarness.Consumed.Any<StockReserved>());
            Assert.NotNull(await sagaHarness.Exists(orderId, x => x.InventoryReserved));

            // Check Command Sent: ProcessPayment
            Assert.True(await harness.Published.Any<ProcessPayment>());

            // Act 3: Simulate Payment Accepted
            await harness.Bus.Publish<PaymentAccepted>(new
            {
                OrderId = orderId
            });

            // Assert 3: Saga Transitions to Completed (and Finalizes/Deletes)
            Assert.True(await sagaHarness.Consumed.Any<PaymentAccepted>());
            
            // Because we use .Finalize(), the instance is removed from the repository.
            // So Exists should return null (or false if it returned bool).
            // assert that OrderCompleted event was published, which implies we reached that state.
            Assert.True(await harness.Published.Any<OrderCompleted>());
            
            // Verify the saga instance is no longer in the repository (simulating Finalize)
            // Note: InMemoryTestHarness behavior on Finalize removes it.
            var exists = await sagaHarness.Exists(orderId, x => x.Completed);
            Assert.Null(exists); 
        }
    }
}
