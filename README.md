# Distributed E-Commerce Order System (RabbitMQ + MassTransit)

This project demonstrates a robust microservices architecture using ASP.NET Core, MassTransit, and RabbitMQ.

## Architecture

- **OrderService**: Web API that accepts orders and hosts the `OrderStateMachine` (Saga). Orchestrates the entire process.
- **InventoryService**: Worker service that listens for `CheckInventory` commands. Simulates stock reservation.
- **PaymentService**: Worker service that listens for `ProcessPayment` commands. Simulates payment processing with random failures and retries.
- **Contracts**: Shared message definitions.

## Key Patterns

1.  **Saga Orchestration**: The `OrderStateMachine` manages the lifecycle: `Submitted` -> `InventoryReserved` -> `Completed` (or `Failed`).
2.  **Request/Reply**: Implicitly handled via the Saga sending commands and waiting for events.
3.  **Retries**: `PaymentService` is configured with a retry policy for transient errors.
4.  **Dead Letter Queue (DLQ)**: Failed messages (e.g. permanent payment failure) will move to `_error` queues.

## How to Run

1.  **Start RabbitMQ**:
    ```bash
    docker-compose up -d
    ```
    *Ensure Docker Desktop is running.*

2.  **Start Services** (Run in separate terminals):
    ```bash
    dotnet run --project InventoryService
    dotnet run --project PaymentService
    dotnet run --project OrderService
    ```

3.  **Submit an Order**:
    ```bash
    curl -X POST http://localhost:5000/Order \
         -H "Content-Type: application/json" \
         -d "{\"customerNumber\": \"12345\", \"totalAmount\": 100.00}"
    ```

4.  **Observe Behavior**:
    - **Happy Path**: Order ID ends in random char (not 0, 1, 2). Saga completes.
    - **Inventory Shortage**: Order ID ends in `0` (Simulated in `InventoryService`).
    - **Payment Hard Fail**: Order ID ends in `1` (Simulated in `PaymentService`).
    - **Payment Retry**: Order ID ends in `2`. `PaymentService` throws, MassTransit retries, eventually fails to DLQ.

## Testing

Run the integration tests using MassTransit TestHarness (In-Memory):
```bash
dotnet test
```
