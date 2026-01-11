# Gemini Context: Distributed E-Commerce Order System

## Project Overview
This is a **distributed microservices application** built with **.NET 10**, simulating an e-commerce order processing workflow. It leverages **MassTransit** for message-based communication and **RabbitMQ** as the message broker. The core architectural pattern is a **Saga (State Machine)** that orchestrates the lifecycle of an order across multiple services.

## Architecture & Technology Stack

*   **Framework**: .NET 10.0 (C# 13)
*   **Messaging**: MassTransit 8.x
*   **Message Broker**: RabbitMQ (running via Docker)
*   **Persistence**: SQLite (Entity Framework Core) for Saga state
*   **Testing**: xUnit with MassTransit TestHarness (In-Memory testing)

### Microservices
1.  **OrderService** (`/OrderService`):
    *   **Type**: ASP.NET Core Web API.
    *   **Role**: Entry point. Accepts `POST /Order`. Hosts the **Order Saga** (`OrderStateMachine`).
    *   **State**: Persists saga state to `orders.db` (SQLite).
2.  **InventoryService** (`/InventoryService`):
    *   **Type**: Worker Service.
    *   **Role**: Consumes `CheckInventory` commands. Simulates stock reservation.
3.  **PaymentService** (`/PaymentService`):
    *   **Type**: Worker Service.
    *   **Role**: Consumes `ProcessPayment` commands. Simulates payment processing with random failure/retry logic.
4.  **Contracts** (`/Contracts`):
    *   **Type**: Class Library.
    *   **Role**: Shared message interfaces (Commands & Events).

## Infrastructure Setup

**RabbitMQ** is required for the services to communicate.

```bash
# Start RabbitMQ container
docker-compose up -d

# Verify Management UI
# http://localhost:15672 (guest/guest)
```

## Build and Run

Run all three services simultaneously (in separate terminals or via IDE compound launch configurations).

```bash
# Terminal 1: Order API & Orchestrator
dotnet run --project OrderService

# Terminal 2: Inventory Worker
dotnet run --project InventoryService

# Terminal 3: Payment Worker
dotnet run --project PaymentService
```

*   **OrderService URL**: `http://localhost:5085` (Swagger at `/swagger`)
*   **DB Location**: `OrderService/orders.db` (Auto-created)

## Usage & Simulation
The system behavior is determined by the **last digit of the Order ID** (GUID).

| GUID Ending | Scenario | Expected Outcome |
| :--- | :--- | :--- |
| **Random** | Happy Path | `Completed` |
| **`0`** | Stock Shortage | `Failed` (Inventory Rejected) |
| **`1`** | Payment Decline | `Failed` (Payment Rejected) |
| **`2`** | Payment Error | Retries x3, then DLQ (Simulates transient error) |

**Sample Request (Happy Path):**
```bash
curl -X POST http://localhost:5085/Order \
  -H "Content-Type: application/json" \
  -d "{\"customerNumber\": \"12345\", \"totalAmount\": 100.00}"
```

## Testing

The project includes integration tests that run **in-memory** (no RabbitMQ required) to verify the Saga logic.

```bash
dotnet test
```

## Key Files & Patterns
*   **`OrderService/Sagas/OrderStateMachine.cs`**: The brain of the operation. Defines the `Event -> State -> Action` transitions.
*   **`Contracts/Messages.cs`**: Defines the API surface between microservices.
*   **`docker-compose.yml`**: Infrastructure definition.
*   **`appsettings.json`**: Configuration for RabbitMQ connection strings.

## Development Conventions
*   **Saga Pattern**: Use `MassTransitStateMachine<OrderState>` for complex workflows.
*   **Contracts**: All messages must be defined in the `Contracts` project as interfaces.
*   **Resilience**: Use `UseMessageRetry` in consumers for transient faults.
*   **Validation**: Logic for "success/failure" is simulated based on input data (GUID suffix) rather than real external calls.
