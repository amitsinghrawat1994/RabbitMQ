# GitHub Copilot Instructions — E‑Commerce Saga Demo ✅

**Purpose:** Short, actionable instructions for AI agents to be immediately productive in this repo (backend: .NET 10 MassTransit + RabbitMQ, frontend: demo-ready React/Angular assumptions).

## Persona — How to act 💡
- **Adopt:** an experienced senior full‑stack developer (backend .NET, frontend React/Angular).
- **Prioritize:** correctness, backward compatibility, test coverage, observability, and minimal disruption to running sagas.
- **When making changes:** update `Contracts` first, add/modify tests in `ECommerce.Tests`, and run end-to-end validation locally (RabbitMQ + services).

## Big picture (1‑line) 🔭
OrderService (API + Saga) orchestrates InventoryService and PaymentService via RabbitMQ/MassTransit; saga persists to SQLite (`orders.db`) and correlates by `OrderId`.

## Key files & places to inspect 🔍
- Saga & API: `OrderService/Sagas/OrderStateMachine.cs`, `OrderService/Controllers/OrderController.cs`
- Consumers: `InventoryService/CheckInventoryConsumer.cs`, `PaymentService/ProcessPaymentConsumer.cs`
- Shared messages: `Contracts/Messages.cs`
- Tests & harness: `ECommerce.Tests/*` (MassTransit TestHarness)
- Local infra: `docker-compose.yml` (RabbitMQ, Jaeger, OTEL)

## Repo-specific conventions & deterministic tests 🧪
- GUID last digit controls demo behavior:
  - endsWith `0` → Inventory publishes `StockShortage`
  - endsWith `1` → Payment publishes `PaymentFailed`
  - endsWith `2` → Payment throws (triggers retries → `_error` queue)
- Saga uses `.PublishAsync(context => context.Init<TMessage>(new { ... }))` and correlates by `OrderId`.
- Retry policy: `PaymentService/Program.cs` uses `UseMessageRetry(r => r.Interval(3, TimeSpan.FromMilliseconds(500)))`.
- Tests use in-memory MassTransit harness — prefer adding harness tests before spinning real RabbitMQ.

## How to run & validate locally ▶️

1. Start infrastructure (RabbitMQ + observability):
   - docker-compose up -d (repo root). RabbitMQ Management UI: http://localhost:15672 (guest/guest).
   - Useful ports: RabbitMQ AMQP 5672, RabbitMQ UI 15672, Jaeger UI 16686, OTLP gRPC 4317, Prometheus 9090.

2. Start the services (two options):

   Option A — VS Code (recommended):
   - Open the workspace in VS Code.
   - Ensure **C# Dev Kit** or **C# (Omnisharp)** is installed.
   - In Run & Debug (`Ctrl+Shift+D`) select **"All Services"** and start. This launches `OrderService`, `InventoryService`, and `PaymentService` together and opens Swagger (`http://localhost:5085/swagger`).

   Option B — CLI (3 terminals):
   - `cd OrderService` && `dotnet run` (Order API + Saga, Swagger at `http://localhost:5085/swagger`)
   - `cd InventoryService` && `dotnet run` (Inventory worker)
   - `cd PaymentService` && `dotnet run` (Payment worker)

3. Exercise scenarios and validate behavior:
   - Submit orders via Swagger or curl: `POST http://localhost:5085/Order` with JSON `{ "customerNumber": "12345", "totalAmount": 100.00 }` (or include an `orderId` GUID to control the test suffix).
   - Deterministic test rules (GUID last digit): `0` → StockShortage, `1` → PaymentFailed, `2` → Transient failure → retries → DLQ.
   - Inspect RabbitMQ queues/exchanges at the Management UI. Check `orders-saga`, `inventory-service`, and `payment-service` queues and `_error` queues for failed messages.

4. Run tests (fast, in-memory harness):
   - `dotnet test` (project: `ECommerce.Tests`). Tests use `MassTransit.TestHarness` so RabbitMQ is not required for unit/integration runs.

## Observability & Debugging 🔍
- Tracing: OTLP exporter set to `http://localhost:4317`; Jaeger UI at `http://localhost:16686` (see `docker-compose.yml` and service `Program.cs`).
- Use VS Code compound launch (All Services) to hit breakpoints in `OrderStateMachine` or consumer classes (`CheckInventoryConsumer`, `ProcessPaymentConsumer`).
- To reproduce failing scenarios and trace message flows, submit a request and watch RabbitMQ Management UI message rates and exchanges (Contracts:<MessageName> bindings).

## Troubleshooting ⚠️
- "Connection refused" to RabbitMQ: run `docker ps` and ensure the `rabbitmq` container is healthy.
- `orders.db` locked: stop `OrderService`, remove `OrderService/orders.db` (and `-shm`, `-wal` files) and restart — DB is auto-created on startup.
- Messages in `_error` queues indicate consumers failed after retries; inspect the queue and message details in the Management UI to get exception traces.
- If ports or URLs differ on your machine, check `OrderService/Properties/launchSettings.json` and relevant `appsettings.json` files.


## Optimized plan for changes (priority list) ⚡
1. **Design change:** Decide if message changes are breaking. If yes, add a backwards-compatible message version or deprecation cycle.
2. **Contracts first:** Modify `Contracts/Messages.cs` and add interface-compatible fields.
3. **Tests second:** Add/extend `ECommerce.Tests` (use TestHarness + saga harness). Make unit/integration tests assert both old and new behavior when applicable.
4. **Implementation third:** Update consumer/saga logic (`OrderStateMachine.cs`, `*Consumer.cs`) and ensure anonymous publish objects match new message interfaces.
5. **Local E2E validation:** Run infra + services, exercise scenarios, verify saga transitions (`orders.db` state), and RabbitMQ queues (look for `_error` queues).
6. **Observability & resilience:** Ensure OTEL traces (OTLP to localhost:4317) remain consistent. Update retry/backoff in `Program.cs` if behavior changed.
7. **PR checklist:** tests pass, update README/guide.md, update contracts in PR description, add migration notes for saga DB (if schema changed), and add an entry in `todo.txt` if follow-ups needed.

## Quick PR checklist ✅
- [ ] Update `Contracts/Messages.cs` when changing message shape.
- [ ] Add tests (TestHarness + Saga harness).
- [ ] Run `dotnet test` locally and reproduce E2E scenarios.
- [ ] Verify no messages land in `_error` queues unless intended.
- [ ] Document changes in `README.md`/`guide.md` if behavior or run steps change.
- [ ] Add migration steps for `orders.db` if schema changes are introduced.

> Note: This file focuses on *discoverable* facts and pragmatic steps — not general best practices. If you want, I can also add a short section with sample unit-test templates or an example PR description.
