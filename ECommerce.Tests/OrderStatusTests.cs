using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Controllers;
using OrderService.Models;
using OrderService.Sagas;
using Xunit;

namespace ECommerce.Tests
{
    public class OrderStatusTests
    {
        [Fact]
        public async Task GetStatus_Returns_Completed()
        {
            var services = new ServiceCollection();
            services.AddDbContext<OrderDbContext>(o => o.UseInMemoryDatabase("status-db-1"));
            await using var provider = services.BuildServiceProvider(true);
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

            var orderId = Guid.NewGuid().ToString();
            db.Orders.Add(new OrderService.Models.Order { OrderId = orderId, Status = "Completed", CreatedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();

            var controller = new OrderController(null!, db);

            var res = await controller.GetStatus(orderId);
            var ok = Assert.IsType<OkObjectResult>(res);
            var value = ok.Value!;
            var propOrderId = value.GetType().GetProperty("OrderId");
            Assert.NotNull(propOrderId);
            Assert.Equal(orderId, propOrderId.GetValue(value)?.ToString());
            var propStatus = value.GetType().GetProperty("Status");
            Assert.NotNull(propStatus);
            Assert.Equal("Completed", propStatus.GetValue(value)?.ToString());
        }

        [Fact]
        public async Task GetStatus_Returns_Failed()
        {
            var services = new ServiceCollection();
            services.AddDbContext<OrderDbContext>(o => o.UseInMemoryDatabase("status-db-2"));
            await using var provider = services.BuildServiceProvider(true);
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

            var orderId = Guid.NewGuid().ToString();
            db.Orders.Add(new OrderService.Models.Order { OrderId = orderId, Status = "Failed", CreatedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow, Reason = "no stock" });
            await db.SaveChangesAsync();

            var controller = new OrderController(null!, db);

            var res = await controller.GetStatus(orderId);
            var ok = Assert.IsType<OkObjectResult>(res);
            var value = ok.Value!;
            var propOrderId = value.GetType().GetProperty("OrderId");
            Assert.NotNull(propOrderId);
            Assert.Equal(orderId, propOrderId.GetValue(value)?.ToString());
            var propStatus = value.GetType().GetProperty("Status");
            Assert.NotNull(propStatus);
            Assert.Equal("Failed", propStatus.GetValue(value)?.ToString());
            var propReason = value.GetType().GetProperty("Reason");
            Assert.NotNull(propReason);
            Assert.Equal("no stock", propReason.GetValue(value)?.ToString());
        }

        [Fact]
        public async Task GetStatus_Returns_InProgress()
        {
            var services = new ServiceCollection();
            services.AddDbContext<OrderDbContext>(o => o.UseInMemoryDatabase("status-db-3"));
            await using var provider = services.BuildServiceProvider(true);
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

            var orderId = Guid.NewGuid().ToString();
            db.Set<OrderState>().Add(new OrderState { CorrelationId = Guid.NewGuid(), OrderId = orderId, CurrentState = "Submitted", Created = DateTime.UtcNow, Updated = DateTime.UtcNow, CustomerNumber = "X", TotalAmount = 10.0m });
            await db.SaveChangesAsync();

            var controller = new OrderController(null!, db);

            var res = await controller.GetStatus(orderId);
            var ok = Assert.IsType<OkObjectResult>(res);
            var value = ok.Value!;
            var propOrderId = value.GetType().GetProperty("OrderId");
            Assert.NotNull(propOrderId);
            Assert.Equal(orderId, propOrderId.GetValue(value)?.ToString());
            var propStatus = value.GetType().GetProperty("Status");
            Assert.NotNull(propStatus);
            Assert.Equal("Submitted", propStatus.GetValue(value)?.ToString());
        }

        [Fact]
        public async Task GetStatus_Returns_NotFound()
        {
            var services = new ServiceCollection();
            services.AddDbContext<OrderDbContext>(o => o.UseInMemoryDatabase("status-db-4"));
            await using var provider = services.BuildServiceProvider(true);
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

            var controller = new OrderController(null!, db);

            var res = await controller.GetStatus(Guid.NewGuid().ToString());
            Assert.IsType<NotFoundResult>(res);
        }
    }
}
