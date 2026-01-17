using System;
using System.Threading.Tasks;
using Contracts;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Controllers;
using OrderService.Models;
using Microsoft.EntityFrameworkCore;
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

            // Create an in-memory OrderDbContext for the controller
            var services2 = new ServiceCollection();
            services2.AddDbContext<OrderService.Sagas.OrderDbContext>(o => o.UseInMemoryDatabase("controller-db"));
            await using var provider2 = services2.BuildServiceProvider(true);
            using var scope2 = provider2.CreateScope();
            var db = scope2.ServiceProvider.GetRequiredService<OrderService.Sagas.OrderDbContext>();

            var controller = new OrderController(harness.Bus, db);
            var request = new OrderRequest
            {
                CustomerNumber = "12345",
                TotalAmount = 99.99m
            };

            // Act - valid request (sanity)
            var result = await controller.SubmitOrder(request);

            // Assert - valid (existing assertions follow)
            var acceptedResult = Assert.IsType<AcceptedResult>(result);
            Assert.NotNull(acceptedResult.Value);

            // Verify message was published
            Assert.True(await harness.Published.Any<OrderSubmitted>());

            // Invalid request: missing CustomerNumber should return BadRequest
            var invalidRequest = new OrderRequest { TotalAmount = 1.0m };
            var badResult = await controller.SubmitOrder(invalidRequest);
            Assert.IsType<BadRequestObjectResult>(badResult);

            // Verify content
            var publishedMessage = harness.Published.Select<OrderSubmitted>().First();
            Assert.Equal(request.CustomerNumber, publishedMessage.Context.Message.CustomerNumber);
            Assert.Equal(request.TotalAmount, publishedMessage.Context.Message.TotalAmount);
            Assert.False(string.IsNullOrEmpty(publishedMessage.Context.Message.OrderId));
            Assert.True(Guid.TryParse(publishedMessage.Context.Message.OrderId, out _));
        }
    }
}