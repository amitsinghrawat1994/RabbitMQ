# Operational Guide: Distributed E-Commerce Order System

This guide provides step-by-step instructions to set up, run, and debug the E-Commerce microservices solution.

## 1. Prerequisites
*   **Docker Desktop**: Required for running RabbitMQ.
*   **.NET 10.0 SDK**: Required to build and run the services.
*   **REST Client**: Postman, curl, or the built-in Swagger UI.

## 2. Infrastructure Setup (RabbitMQ)

Before starting any .NET services, the messaging infrastructure must be up.

1.  Open a terminal in the solution root (`D:\Personal\Azure_Testing\RabbitMQ`).
2.  Run the following command to start RabbitMQ:
    ```powershell
    docker-compose up -d
    ```
3.  **Verify**: Open your browser to [http://localhost:15672](http://localhost:15672).
    *   **User**: `guest`
    *   **Password**: `guest`
    *   *Result*: You should see the RabbitMQ Management Dashboard.

## 3. Running the Microservices

You need to run all three services simultaneously.

### Option A: Using VS Code (Recommended)
1.  Open the project in **VS Code**.
2.  Ensure you have the **C# Dev Kit** or **C#** (Omnisharp) extension installed.
3.  Go to the **Run and Debug** view (`Ctrl+Shift+D`).
4.  From the dropdown at the top, select **"All Services"**.
5.  Click the **Green Arrow (Start Debugging)**.
    *   This will launch `OrderService`, `InventoryService`, and `PaymentService` simultaneously.
    *   The `OrderService` will automatically open the browser to the Swagger UI.

### Option B: Using Terminal (CLI)
Open **three separate terminal windows** and run:

**Terminal 1 (Order Service):**
```powershell
cd OrderService
dotnet run
```
*Note: This service will host the Swagger UI at `http://localhost:5085/swagger`.*

**Terminal 2 (Inventory Service):**
```powershell
cd InventoryService
dotnet run
```

**Terminal 3 (Payment Service):**
```powershell
cd PaymentService
dotnet run
```

## 4. Debugging & Testing Flow

We will simulate different order scenarios to verify the Saga orchestration.

### Scenario A: The Happy Path (Success)
**Goal**: Order -> Reserved -> Paid -> Completed.

1.  **Trigger API**:
    *   Open Swagger: [http://localhost:5085/swagger](http://localhost:5085/swagger)
    *   Expand `POST /Order`.
    *   Click **Try it out**.
    *   **Payload**: Use a GUID that **does NOT** end in `0`, `1`, or `2`.
        ```json
        {
          "orderId": "3fa85f64-5717-4562-b3fc-2c963f66afa3",
          "customerNumber": "12345"
        }
        ```
    *   Click **Execute**.

2.  **Verify RabbitMQ**:
    *   Go to [RabbitMQ Queues](http://localhost:15672/#/queues).
    *   Observe traffic flowing through:
        *   `inventory-service` (Consumed CheckInventory)
        *   `payment-service` (Consumed ProcessPayment)
        *   `orders-saga` (Orchestrated the events)

3.  **Verify Logs**:
    *   **OrderService**: Should show `Transitioned to Completed`.
    *   **InventoryService**: Should log `Reserving stock for order...`.
    *   **PaymentService**: Should log `Processing payment...`.

---

### Scenario B: Stock Shortage (Inventory Fail)
**Goal**: Order -> Stock Shortage -> Failed.

1.  **Trigger API**: Use a GUID ending in **`0`**.
    ```json
    {
      "orderId": "3fa85f64-5717-4562-b3fc-2c963f66afa0",
      "customerNumber": "12345"
    }
    ```
2.  **Observation**:
    *   **InventoryService** logs: `Stock shortage for order...`.
    *   **PaymentService**: Will **NOT** receive any message (Saga stops before payment).
    *   **OrderService**: Saga state becomes `Failed`.

---

### Scenario C: Payment Declined (Payment Fail)
**Goal**: Order -> Reserved -> Payment Failed -> Failed.

1.  **Trigger API**: Use a GUID ending in **`1`**.
    ```json
    {
      "orderId": "3fa85f64-5717-4562-b3fc-2c963f66afa1",
      "customerNumber": "12345"
    }
    ```
2.  **Observation**:
    *   **InventoryService**: Reserves stock.
    *   **PaymentService**: Logs `Payment rejected...`.
    *   **OrderService**: Saga state becomes `Failed`.

---

### Scenario D: Transient Failure (Retry Policy)
**Goal**: Order -> Reserved -> Payment Retry (x3) -> Completed (or Failed).

1.  **Trigger API**: Use a GUID ending in **`2`**.
    ```json
    {
      "orderId": "3fa85f64-5717-4562-b3fc-2c963f66afa2",
      "customerNumber": "12345"
    }
    ```
2.  **Observation**:
    *   **PaymentService**: You will see it "crash" (simulated exception) and restart processing the *same message* multiple times immediately.
    *   This demonstrates the `UseMessageRetry` configuration in `Program.cs`.

## 5. Troubleshooting

*   **"Connection Refused" to RabbitMQ**: Ensure Docker container is running (`docker ps`).
*   **Database Locks**: If `OrderService` crashes, `orders.db` might be locked. Delete the `orders.db` file (it will be recreated) or stop the process holding it.
*   **Messages in "_error" queues**: This means a consumer failed to process a message even after retries. Check the queue in RabbitMQ to see the exception details.

## 6. Understanding the Saga Pattern

### What is a Saga?
A **Saga** is a design pattern used to manage data consistency across microservices in distributed transaction scenarios. Unlike a traditional monolithic application where a single ACID database transaction can handle all updates (e.g., update inventory *and* create order), microservices typically use a **database-per-service** model. This prevents a single global transaction from spanning multiple services.

### How does it work?
Instead of one long transaction, a Saga breaks the business process into a sequence of **local transactions**.
1.  **Sequence of Events**: The Saga orchestrator (in our case, the `OrderStateMachine` in `OrderService`) listens for events and triggers the next step.
    *   *Example*: `OrderSubmitted` -> Trigger `CheckInventory`.
2.  **State Management**: The Saga maintains the current state of the process (e.g., `Submitted`, `InventoryReserved`, `Paid`, `Completed`).
3.  **Compensating Transactions**: If a step fails, the Saga executes **compensating actions** to undo the changes made by previous steps.
    *   *Example*: If `ProcessPayment` fails, the Saga would send a command to `ReleaseInventory` to revert the stock reservation, ensuring the system returns to a consistent state.

### Why do we need it?
*   **Consistency**: Ensures that even if a part of the process fails (e.g., payment server down), the system doesn't end up with "half-finished" data (e.g., stock reserved but order never paid).
*   **Decoupling**: Services (Inventory, Payment) don't need to know about each other. They simply respond to commands and publish events. The Saga handles the complex coordination logic centrally.
*   **Resilience**: Allows long-running processes to pause and resume (e.g., waiting for a payment provider webhook) without holding open database connections.

### 6.4 Debugging Stock Failure in RabbitMQ Dashboard

Follow these steps to visualize the "Stock Failure" scenario (Scenario B) directly in RabbitMQ.

1.  **Open Dashboard**: Go to [http://localhost:15672](http://localhost:15672) (User/Pass: `guest`/`guest`).
2.  **Reset State**: Ideally, ensure no other tests are running.
3.  **Trigger the Failure**: 
    *   Send the POST request with an ID ending in `0` (e.g., `...-0000`).
4.  **Observe the "Overview" Tab**:
    *   Look at the **Message Rates** graph. You should see a quick spike.
    *   **Publish**: The `OrderService` publishing `OrderSubmitted`.
    *   **Deliver**: The message being delivered to `InventoryService`.
    *   **Ack**: The `InventoryService` acknowledging receipt.
5.  **Inspect Queues (The Trace)**:
    *   Click the **Queues** tab.
    *   **`inventory-service`**:
        *   You should see the `Incoming` rate spike briefly as it processes `CheckInventory`.
    *   **`payment-service`**:
        *   **CRITICAL**: This queue should remain **silent** (0 messages). This proves the Saga successfully halted the process.
    *   **`orders-saga`**:
        *   This queue receives the events `OrderSubmitted` and then `StockShortage`.
6.  **Verify the Exchange Routing (Advanced)**:
    *   Click the **Exchanges** tab.
    *   Click on **`Contracts:StockShortage`** (created by the Inventory Service).
    *   Scroll to **Bindings**.
    *   You should see it bound to the `orders-saga` queue. This confirms that when Inventory fails, the message is correctly routed back to the Saga.

