using System;
using System.Threading.Tasks;
using Contracts;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Controllers;
using OrderService.Models;
using Xunit;

namespace ECommerce.Tests
{
    public class OrderControllerTests
    {
        [Fact]
        public async Task SubmitOrder_Should_Publish_OrderSubmitted_And_Return_Accepted()
        {
            // Arrange
            await using var provider = new ServiceCollection()
                .AddMassTransitTestHarness()
                .BuildServiceProvider(true);

            var harness = provider.GetRequiredService<ITestHarness>();
            await harness.Start();

            // Inject the harness bus into the controller
            var controller = new OrderController(harness.Bus);
            var request = new OrderRequest
            {
                CustomerNumber = "12345",
                TotalAmount = 99.99m
            };

            // Act
            var result = await controller.SubmitOrder(request);

            // Assert
            var acceptedResult = Assert.IsType<AcceptedResult>(result);
            Assert.NotNull(acceptedResult.Value);

            // Verify message was published
            Assert.True(await harness.Published.Any<OrderSubmitted>());

            // Verify content
            var publishedMessage = harness.Published.Select<OrderSubmitted>().First();
            Assert.Equal(request.CustomerNumber, publishedMessage.Context.Message.CustomerNumber);
            Assert.Equal(request.TotalAmount, publishedMessage.Context.Message.TotalAmount);
            Assert.NotEqual(Guid.Empty, publishedMessage.Context.Message.OrderId);
        }
    }
}