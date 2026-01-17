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
    public class OrderCompletedConsumer : IConsumer<OrderCompleted>
    {
        private readonly OrderDbContext _db;
        private readonly ILogger<OrderCompletedConsumer> _logger;

        public OrderCompletedConsumer(OrderDbContext db, ILogger<OrderCompletedConsumer> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<OrderCompleted> context)
        {
            var orderId = context.Message.OrderId;
            _logger.LogInformation("Persisting completed order {OrderId}", orderId);

            // Idempotent upsert
            var existing = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (existing == null)
            {
                _db.Orders.Add(new Order
                {
                    OrderId = orderId,
                    Status = "Completed",
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.Status = "Completed";
                existing.CompletedAt = DateTime.UtcNow;
                existing.Reason = null;
            }

            await _db.SaveChangesAsync();
        }
    }
}
