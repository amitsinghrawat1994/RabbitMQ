using System;

namespace Contracts
{
    // Event: Triggered when a user places an order via the API
    public interface OrderSubmitted
    {
        string OrderId { get; }
        DateTime Timestamp { get; }
        string CustomerNumber { get; }
        decimal TotalAmount { get; }
    }

    // Command: Saga asks Inventory Service to check stock
    public interface CheckInventory
    {
        string OrderId { get; }
    }

    // Event: Inventory successfully reserved
    public interface StockReserved
    {
        string OrderId { get; }
    }

    // Event: Inventory failed
    public interface StockShortage
    {
        string OrderId { get; }
        string Reason { get; }
    }

    // Command: Saga asks Payment Service to charge the customer
    public interface ProcessPayment
    {
        string OrderId { get; }
        decimal Amount { get; }
        string CardNumber { get; }
    }

    // Event: Payment successful
    public interface PaymentAccepted
    {
        string OrderId { get; }
    }

    // Event: Payment failed
    public interface PaymentFailed
    {
        string OrderId { get; }
        string Reason { get; }
    }

    // Event: Order processing timeout
    public interface OrderTimeoutExpired
    {
        string OrderId { get; }
    }

    // Event: Happy path completion
    public interface OrderCompleted
    {
        string OrderId { get; }
    }

    // Event: Sad path completion
    public interface OrderFailed
    {
        string OrderId { get; }
        string Reason { get; }
    }
}
