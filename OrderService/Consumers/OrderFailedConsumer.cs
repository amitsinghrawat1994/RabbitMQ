using System;
using System.Threading.Tasks;
using Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderService.Models;
using OrderService.Sagas;

namespace OrderService
{
    public class OrderFailedConsumer : IConsumer<OrderFailed>
    {
        private readonly OrderDbContext _db;
        private readonly ILogger<OrderFailedConsumer> _logger;

        public OrderFailedConsumer(OrderDbContext db, ILogger<OrderFailedConsumer> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<OrderFailed> context)
        {
            var orderId = context.Message.OrderId;
            _logger.LogInformation("Persisting failed order {OrderId}", orderId);

            // Idempotent upsert
            var existing = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (existing == null)
            {
                _db.Orders.Add(new Order
                {
                    OrderId = orderId,
                    Status = "Failed",
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow,
                    Reason = context.Message.Reason
                });
            }
            else
            {
                existing.Status = "Failed";
                existing.CompletedAt = DateTime.UtcNow;
                existing.Reason = context.Message.Reason;
            }

            await _db.SaveChangesAsync();
        }
    }
}
