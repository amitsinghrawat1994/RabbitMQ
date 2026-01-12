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
1. Start infra: `docker-compose up -d` (repo root). RabbitMQ UI: http://localhost:15672 (guest/guest).
2. Start services (either VS Code "All Services" launch or CLI):
   - `dotnet run --project OrderService`
   - `dotnet run --project InventoryService`
   - `dotnet run --project PaymentService`
3. Reproduce scenarios using Swagger or POST `/Order`. Use GUIDs to exercise `0/1/2` behaviors.
4. Run tests: `dotnet test` (runs fast with TestHarness).

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
