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
