using System;
using System.Threading.Tasks;
using Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace PaymentService
{
    public class ProcessPaymentConsumer : IConsumer<ProcessPayment>
    {
        private readonly ILogger<ProcessPaymentConsumer> _logger;

        public ProcessPaymentConsumer(ILogger<ProcessPaymentConsumer> logger)
        {
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<ProcessPayment> context)
        {
            var orderId = context.Message.OrderId;
            var amount = context.Message.Amount;

            _logger.LogInformation("Processing payment for Order: {OrderId}, Amount: {Amount}", orderId, amount);

            var guidString = orderId;

            // 1. Logic: Hard Fail
            if (guidString.EndsWith("1"))
            {
                _logger.LogWarning("Payment rejected for Order: {OrderId}", orderId);
                await context.Publish<PaymentFailed>(new
                {
                    OrderId = orderId,
                    Reason = "Credit card limit exceeded"
                });
                return;
            }

            // 2. Logic: Transient Failure (to demonstrate Retry)
            // We use a static counter or random to ensure it eventually passes, 
            // but for a strict 'EndsWith 2' test, it would retry according to the configured retry policy and then end up in the _error queue if it keeps failing.
            // Let's let it go to DLQ for '2'.
            if (guidString.EndsWith("2"))
            {
                _logger.LogError("Payment service transient failure for Order: {OrderId}", orderId);
                throw new InvalidOperationException("Payment Gateway Unavailable (Simulated)");
            }

            // 3. Success
            _logger.LogInformation("Payment accepted for Order: {OrderId}", orderId);
            await context.Publish<PaymentAccepted>(new
            {
                OrderId = orderId
            });
        }
    }
}
