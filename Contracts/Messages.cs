using System;

namespace Contracts
{
    // Event: Triggered when a user places an order via the API
    public interface OrderSubmitted
    {
        Guid OrderId { get; }
        DateTime Timestamp { get; }
        string CustomerNumber { get; }
        decimal TotalAmount { get; }
    }

    // Command: Saga asks Inventory Service to check stock
    public interface CheckInventory
    {
        Guid OrderId { get; }
    }

    // Event: Inventory successfully reserved
    public interface StockReserved
    {
        Guid OrderId { get; }
    }

    // Event: Inventory failed
    public interface StockShortage
    {
        Guid OrderId { get; }
        string Reason { get; }
    }

    // Command: Saga asks Payment Service to charge the customer
    public interface ProcessPayment
    {
        Guid OrderId { get; }
        decimal Amount { get; }
        string CardNumber { get; }
    }

    // Event: Payment successful
    public interface PaymentAccepted
    {
        Guid OrderId { get; }
    }

    // Event: Payment failed
    public interface PaymentFailed
    {
        Guid OrderId { get; }
        string Reason { get; }
    }

    // Event: Happy path completion
    public interface OrderCompleted
    {
        Guid OrderId { get; }
    }

    // Event: Sad path completion
    public interface OrderFailed
    {
        Guid OrderId { get; }
        string Reason { get; }
    }
}
