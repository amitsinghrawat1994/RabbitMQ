using System;
using System.Threading.Tasks;
using Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InventoryService
{
    public class CheckInventoryConsumer : IConsumer<CheckInventory>
    {
        private readonly ILogger<CheckInventoryConsumer> _logger;

        public CheckInventoryConsumer(ILogger<CheckInventoryConsumer> logger)
        {
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<CheckInventory> context)
        {
            var orderId = context.Message.OrderId;
            _logger.LogInformation("Checking inventory for Order: {OrderId}", orderId);

            // Simulate processing time
            await Task.Delay(500);

            // Simple Logic: 
            // If the OrderId ends in '0', simulate shortage.
            // Otherwise, reserve stock.
            // This is deterministic for testing, but 'random' enough for a demo.

            // OrderId is a string — check the last char
            var guidString = orderId;

            if (guidString.EndsWith("0"))
            {
                _logger.LogWarning("Stock shortage for Order: {OrderId}", orderId);

                await context.Publish<StockShortage>(new
                {
                    OrderId = orderId,
                    Reason = "Item out of stock"
                });
            }
            else
            {
                _logger.LogInformation("Stock reserved for Order: {OrderId}", orderId);

                await context.Publish<StockReserved>(new
                {
                    OrderId = orderId
                });
            }
        }
    }
}
