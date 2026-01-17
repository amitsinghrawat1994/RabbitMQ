# Error & Exception Handling Analysis — E-Commerce Saga System

**Date**: January 17, 2026  
**Project**: Distributed E-Commerce Order Orchestration (RabbitMQ + MassTransit)

---

## Executive Summary

Your current architecture has a **solid foundation** for error handling with saga state transitions and event-driven failures, but there are **critical gaps** in resilience, observability, and error recovery. This document provides:

1. **Current State Assessment** — what's working ✅
2. **Identified Gaps** — what's missing ⚠️
3. **Recommendations** — actionable improvements 🚀

---

## 1. Current Error Handling Strategy

### ✅ What's Working Well

#### A. **Saga-Level Failure Handling**
- **Status**: `Submitted` → `Failed` (via `StockShortage` or `PaymentFailed` events)
- **Location**: [OrderService/Sagas/OrderStateMachine.cs](OrderService/Sagas/OrderStateMachine.cs#L44-L54)
- **Behavior**: Saga publishes `OrderFailed` event with a `Reason` field
- **Persistence**: [OrderFailedConsumer.cs](OrderService/Consumers/OrderFailedConsumer.cs) captures failed state to `Orders` table

```csharp
// Example: Stock shortage triggers failure path
When(StockShortage)
    .Then(context => context.Saga.Updated = DateTime.UtcNow)
    .TransitionTo(Failed)
    .PublishAsync(context => context.Init<OrderFailed>(new
    {
        OrderId = context.Saga.OrderId,
        Reason = context.Message.Reason
    }))
```

#### B. **Known Failure Scenarios** (Deterministic Tests)
Three controlled failure paths via GUID last digit:
- **EndsWith `0`**: Inventory shortage (intentional business failure)
- **EndsWith `1`**: Payment declined (intentional business failure)
- **EndsWith `2`**: Payment gateway error (transient, triggers retries)

#### C. **Consumer-Level Logging**
All consumers log at appropriate levels (`Info`, `Warning`, `Error`):
```csharp
_logger.LogInformation("Processing payment for Order: {OrderId}, Amount: {Amount}", orderId, amount);
_logger.LogWarning("Payment rejected for Order: {OrderId}", orderId);
_logger.LogError("Payment service transient failure for Order: {OrderId}", orderId);
```

#### D. **Retry Policy** (Partial)
[PaymentService/Program.cs](PaymentService/Program.cs#L34) has a basic retry configuration:
```csharp
cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromMilliseconds(500)));
```
- **Scope**: Payment consumer only
- **Policy**: 3 retries at 500ms intervals
- **Limitation**: No exponential backoff; InventoryService has **no retry policy**

#### E. **Dead Letter Queue (DLQ)**
MassTransit automatically routes permanently failed messages to `_error` queues:
- `orders-saga_error`
- `inventory-service_error`
- `payment-service_error`

---

## 2. Critical Gaps & Issues

### ⚠️ Gap 1: **Incomplete Retry Configuration**

**Problem**:
- Only `PaymentService` has retries; `InventoryService` has **none**
- Retry policy uses **fixed 500ms interval** (no exponential backoff)
- No per-message-type retry strategies
- Transient failures in inventory checks go directly to DLQ instead of retrying

**Impact**: 
- Temporary network blips → permanent order failures
- No resilience for slow external systems

**Example**:
```csharp
// InventoryService/Program.cs — NO RETRY POLICY
x.UsingRabbitMq((context, cfg) =>
{
    cfg.Host(...);
    // ❌ Missing: cfg.UseMessageRetry(...)
    cfg.ConfigureEndpoints(context);
});
```

---

### ⚠️ Gap 2: **No Timeout Handling in Saga**

**Problem**:
- Saga waits indefinitely for events (no timeout configured)
- If `CheckInventory` is sent but `StockReserved` or `StockShortage` never arrives, saga stays in `Submitted` state forever
- Same issue for payment responses

**Current Code**:
```csharp
During(Submitted,
    When(StockReserved) // ← what if this never comes?
        .Then(...)
        .TransitionTo(InventoryReserved)
        .Publish(...),

    When(StockShortage)
        .Then(...)
        .TransitionTo(Failed)
        .Publish(...)
);
// ❌ No timeout or "time elapses" trigger
```

**Impact**: 
- Orphaned saga instances consume memory
- No automatic compensation or user notification
- Hard to detect stuck orders

---

### ⚠️ Gap 3: **No Exception Handling in API Controller**

**Problem**:
- [OrderController.cs](OrderService/Controllers/OrderController.cs) has validation but no `try-catch` for publishing
- Publishing can throw (RabbitMQ connection lost, serialization error)
- Client gets a 500 instead of meaningful error

**Current Code**:
```csharp
[HttpPost]
public async Task<IActionResult> SubmitOrder([FromBody] OrderRequest request)
{
    // ✅ Validation exists
    // ❌ No try-catch for publish
    await _publishEndpoint.Publish<OrderSubmitted>(new { ... });
    return Accepted(new { OrderId = orderId });
}
```

**Impact**:
- API crashes on infrastructure errors
- No graceful degradation
- Client can't distinguish between validation errors and system errors

---

### ⚠️ Gap 4: **Missing Compensation Logic**

**Problem**:
- When payment fails, there's **no rollback** of inventory reservation
- If saga transitions to `Failed`, inventory remains locked (unless service cleans up)
- No explicit compensation pattern

**Current Behavior**:
```csharp
// If payment fails, saga publishes OrderFailed and transitions to Failed
When(PaymentFailed)
    .Then(context => context.Saga.Updated = DateTime.UtcNow)
    .TransitionTo(Failed)
    .PublishAsync(context => context.Init<OrderFailed>(...))
    // ❌ No "ReleaseInventory" command sent to InventoryService
```

**Impact**:
- Inventory appears reserved but order is failed → inconsistent state
- Manual intervention required to clean up

---

### ⚠️ Gap 5: **Limited Exception Type Differentiation**

**Problem**:
- [PaymentService/ProcessPaymentConsumer.cs](PaymentService/ProcessPaymentConsumer.cs) throws generic `InvalidOperationException`
- No distinction between **transient** (retry-worthy) vs. **permanent** (DLQ-worthy) errors
- Consumer code doesn't use MassTransit exception handling filters

**Current Code**:
```csharp
if (guidString.EndsWith("2"))
{
    _logger.LogError("Payment service transient failure...");
    throw new InvalidOperationException("Payment Gateway Unavailable (Simulated)");
    // ↑ Generic exception; MassTransit can't distinguish failure type
}
```

**Impact**:
- All exceptions trigger same retry policy
- No ability to skip retries for non-recoverable errors
- Harder to diagnose root cause from logs

---

### ⚠️ Gap 6: **No Circuit Breaker Pattern**

**Problem**:
- No circuit breaker for failing services
- If payment service is consistently failing, we keep retrying and queuing messages
- Adds unnecessary load to failing service

**Impact**:
- Cascading failures across the system
- Message queue backs up
- Slow recovery when downstream service recovers

---

### ⚠️ Gap 7: **Incomplete Error Observability**

**Problem**:
- Logs exist but no structured context capture (correlation ID, retry count)
- No easy way to track a message through retries
- OTEL tracing enabled but exception details may not propagate

**Missing Context**:
- Retry attempt number
- Message correlation ID (MassTransit provides this but not logged explicitly)
- Full exception stack traces in warning/error logs
- Business context (customer ID, order amount) in error logs

---

### ⚠️ Gap 8: **Database Persistence Gaps**

**Problem**:
- `OrderFailedConsumer` catches `OrderFailed` events, but **not all failure paths are captured**:
  - Saga creation fails → no `Order` record created
  - Message serialization fails → no record
  - Consumer crash during persistence → inconsistent state

**Current Implementation**:
```csharp
// OrderFailedConsumer is idempotent (upsert) but:
// - Only triggered if OrderFailed event is successfully published
// - If saga crashes before publishing, no audit trail
```

---

## 3. Recommended Improvements (Priority Order)

### 🔴 **Priority 1: Critical Production Risk**

#### 1.1 Add Retry Policies to All Services

**Action**: Add exponential backoff retries to `InventoryService` and upgrade `PaymentService`:

```csharp
// Both InventoryService/Program.cs and PaymentService/Program.cs
cfg.UseMessageRetry(r =>
{
    r.Incremental(
        retryLimit: 5,                      // Up to 5 retries
        initialInterval: TimeSpan.FromMilliseconds(100),
        intervalIncrement: TimeSpan.FromMilliseconds(100)
    );
    // Alternative: exponential backoff
    // r.Exponential(
    //     retryLimit: 5,
    //     minInterval: TimeSpan.FromMilliseconds(100),
    //     maxInterval: TimeSpan.FromSeconds(10),
    //     intervalDelta: TimeSpan.FromMilliseconds(50)
    // );
});
```

**Files to Update**:
- `InventoryService/Program.cs` (add retry)
- `PaymentService/Program.cs` (upgrade to incremental/exponential)
- Consider adding `UseMessageRetry()` to OrderService saga endpoint if needed

---

#### 1.2 Add Saga Timeouts

**Action**: Configure timeout and compensation in [OrderStateMachine.cs](OrderService/Sagas/OrderStateMachine.cs):

```csharp
public class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    public OrderStateMachine()
    {
        // Add a scheduled event
        Schedule(() => OrderTimeout, 
            instance => instance.CorrelationId, 
            cfg => cfg.Delay = TimeSpan.FromMinutes(5)); // 5-min timeout

        Initially(
            When(OrderSubmitted)
                .Then(context => {
                    context.Saga.OrderId = context.Message.OrderId;
                    context.Saga.Created = DateTime.UtcNow;
                    context.Saga.Updated = DateTime.UtcNow;
                    context.Saga.CustomerNumber = context.Message.CustomerNumber;
                    context.Saga.TotalAmount = context.Message.TotalAmount;
                })
                .Schedule(OrderTimeout, context => new OrderTimeoutExpired { OrderId = context.Saga.OrderId })
                .TransitionTo(Submitted)
                .PublishAsync(context => context.Init<CheckInventory>(new { OrderId = context.Saga.OrderId }))
        );

        During(Submitted,
            // ✅ NEW: Timeout handler
            When(OrderTimeout)
                .Then(context => {
                    context.Saga.Updated = DateTime.UtcNow;
                })
                .TransitionTo(Failed)
                .PublishAsync(context => context.Init<OrderFailed>(new {
                    OrderId = context.Saga.OrderId,
                    Reason = "Order processing timeout — no response from inventory service"
                }))
        );
        
        // ... rest of saga configuration
    }

    public State Submitted { get; private set; } = null!;
    public State InventoryReserved { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State Failed { get; private set; } = null!;

    public Schedule<OrderState, OrderTimeoutExpired> OrderTimeout { get; private set; } = null!;
    // ... other events
}
```

**Update `Contracts/Messages.cs`** to add:
```csharp
public interface OrderTimeoutExpired
{
    string OrderId { get; }
}
```

**Update `OrderState` model** to track timeout:
```csharp
public Guid? TimeoutTokenId { get; set; }
```

---

#### 1.3 Add Exception Handling in API Controller

**Action**: Wrap publish in try-catch and return appropriate status codes:

```csharp
[HttpPost]
public async Task<IActionResult> SubmitOrder([FromBody] OrderRequest request)
{
    // Validation
    var validationContext = new ValidationContext(request);
    var validationResults = new List<ValidationResult>();
    if (!Validator.TryValidateObject(request, validationContext, validationResults, true))
    {
        foreach (var vr in validationResults)
        {
            var member = vr.MemberNames != null ? string.Join(",", vr.MemberNames) : string.Empty;
            ModelState.AddModelError(member, vr.ErrorMessage ?? "Invalid");
        }
        return BadRequest(ModelState);
    }

    var orderId = request.OrderId ?? Guid.NewGuid().ToString();

    try
    {
        await _publishEndpoint.Publish<OrderSubmitted>(new
        {
            OrderId = orderId,
            Timestamp = DateTime.UtcNow,
            CustomerNumber = request.CustomerNumber,
            TotalAmount = request.TotalAmount
        });

        return Accepted(new { OrderId = orderId });
    }
    catch (Exception ex)
    {
        // Log with full context
        _logger.LogError(ex, "Failed to publish OrderSubmitted for OrderId: {OrderId}", orderId);
        
        // Return error without exposing internal details
        return StatusCode(503, new { error = "Service unavailable. Please try again later." });
    }
}
```

**Inject ILogger<OrderController>** in constructor:
```csharp
private readonly ILogger<OrderController> _logger;

public OrderController(IPublishEndpoint publishEndpoint, OrderDbContext db, ILogger<OrderController> logger)
{
    _publishEndpoint = publishEndpoint;
    _db = db;
    _logger = logger;
}
```

---

### 🟠 **Priority 2: High-Impact Improvements**

#### 2.1 Add Compensation Logic (Inventory Release)

**Action**: Add `ReleaseInventory` message and handler:

**Update `Contracts/Messages.cs`**:
```csharp
// Command: Release previously reserved inventory
public interface ReleaseInventory
{
    string OrderId { get; }
}
```

**Update `OrderStateMachine.cs`** to release inventory on failure:
```csharp
When(PaymentFailed)
    .Then(context => context.Saga.Updated = DateTime.UtcNow)
    .TransitionTo(Failed)
    .PublishAsync(context => context.Init<ReleaseInventory>(new { OrderId = context.Saga.OrderId }))
    .PublishAsync(context => context.Init<OrderFailed>(new {
        OrderId = context.Saga.OrderId,
        Reason = context.Message.Reason
    }))
```

**Add handler in `InventoryService/ReleaseInventoryConsumer.cs`**:
```csharp
public class ReleaseInventoryConsumer : IConsumer<ReleaseInventory>
{
    private readonly ILogger<ReleaseInventoryConsumer> _logger;

    public ReleaseInventoryConsumer(ILogger<ReleaseInventoryConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ReleaseInventory> context)
    {
        var orderId = context.Message.OrderId;
        _logger.LogInformation("Releasing inventory for Order: {OrderId}", orderId);
        // In production: call inventory management service
        // For now, just log (inventory is simulated)
        await Task.CompletedTask;
    }
}
```

---

#### 2.2 Differentiate Exception Types

**Action**: Create custom exception types and use MassTransit exception filters:

**Create `PaymentService/Exceptions/PaymentException.cs`**:
```csharp
namespace PaymentService.Exceptions
{
    public class PaymentException : Exception
    {
        public bool IsTransient { get; set; }
        public PaymentException(string message, bool isTransient = true) : base(message)
        {
            IsTransient = isTransient;
        }
    }

    public class PaymentDeclinedException : PaymentException
    {
        public PaymentDeclinedException() : base("Payment declined", isTransient: false) { }
    }

    public class PaymentGatewayException : PaymentException
    {
        public PaymentGatewayException() : base("Payment gateway unavailable", isTransient: true) { }
    }
}
```

**Update `ProcessPaymentConsumer.cs`**:
```csharp
if (guidString.EndsWith("1"))
{
    _logger.LogWarning("Payment rejected for Order: {OrderId}", orderId);
    throw new PaymentDeclinedException();
}

if (guidString.EndsWith("2"))
{
    _logger.LogError("Payment gateway failure for Order: {OrderId}", orderId);
    throw new PaymentGatewayException();
}
```

**Configure exception filters in `PaymentService/Program.cs`**:
```csharp
x.UsingRabbitMq((context, cfg) =>
{
    var rabbitConfig = context.GetRequiredService<IConfiguration>().GetSection("RabbitMQ");
    cfg.Host(rabbitConfig["Host"] ?? "localhost", "/", h =>
    {
        h.Username(rabbitConfig["Username"] ?? "guest");
        h.Password(rabbitConfig["Password"] ?? "guest");
    });

    // ✅ NEW: Configure retry with exception filter
    cfg.UseMessageRetry(r =>
    {
        r.Handle<PaymentGatewayException>();
        r.Incremental(
            retryLimit: 5,
            initialInterval: TimeSpan.FromMilliseconds(100),
            intervalIncrement: TimeSpan.FromMilliseconds(100)
        );
    });

    // Skip retries for permanent failures
    cfg.UseExceptionHandler(exceptionConfig =>
    {
        exceptionConfig.Handle<PaymentDeclinedException>(context =>
        {
            _logger.LogWarning("Payment declined - not retrying for Order: {OrderId}", context.OrderId);
            // Could publish event here if needed
            return true; // Mark as handled, send to DLQ
        });
    });

    cfg.ConfigureEndpoints(context);
});
```

---

#### 2.3 Add Circuit Breaker

**Action**: Implement circuit breaker for payment and inventory services:

```csharp
// In PaymentService/Program.cs and InventoryService/Program.cs
x.UsingRabbitMq((context, cfg) =>
{
    var rabbitConfig = context.GetRequiredService<IConfiguration>().GetSection("RabbitMQ");
    cfg.Host(rabbitConfig["Host"] ?? "localhost", "/", h =>
    {
        h.Username(rabbitConfig["Username"] ?? "guest");
        h.Password(rabbitConfig["Password"] ?? "guest");
    });

    // Circuit breaker: fail fast after N consecutive failures
    cfg.UseCircuitBreaker(cb =>
    {
        cb.TrackingPeriod = TimeSpan.FromSeconds(30);
        cb.TripThreshold = 5;           // Trip after 5 failures
        cb.ActiveThreshold = 10;        // Re-enable after 10 successes
        cb.ResetInterval = TimeSpan.FromSeconds(30);
    });

    cfg.UseMessageRetry(r =>
    {
        r.Incremental(5, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
    });

    cfg.ConfigureEndpoints(context);
});
```

---

### 🟡 **Priority 3: Observability & Debugging**

#### 3.1 Structured Logging with Context

**Action**: Enhance all consumers with structured logging:

```csharp
// Example: PaymentService/ProcessPaymentConsumer.cs
public async Task Consume(ConsumeContext<ProcessPayment> context)
{
    var orderId = context.Message.OrderId;
    var correlationId = context.CorrelationId;  // MassTransit provides this
    var attemptCount = context.Headers.Get<int>("Attempt-Count", 0) + 1;

    using var logScope = _logger.BeginScope(new Dictionary<string, object>
    {
        ["OrderId"] = orderId,
        ["CorrelationId"] = correlationId,
        ["AttemptCount"] = attemptCount,
        ["Amount"] = context.Message.Amount
    });

    _logger.LogInformation("Processing payment for Order: {OrderId}, Attempt: {AttemptCount}", 
        orderId, attemptCount);

    try
    {
        // ... business logic
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Payment processing failed for Order: {OrderId} on attempt {AttemptCount}",
            orderId, attemptCount);
        throw;
    }
}
```

**Also update OrderController**:
```csharp
try
{
    var correlationId = Guid.NewGuid();
    await _publishEndpoint.Publish<OrderSubmitted>(new { ... }, context =>
    {
        context.CorrelationId = correlationId;
    });

    using var logScope = _logger.BeginScope(new Dictionary<string, object>
    {
        ["OrderId"] = orderId,
        ["CorrelationId"] = correlationId,
        ["CustomerNumber"] = request.CustomerNumber,
        ["Amount"] = request.TotalAmount
    });

    _logger.LogInformation("Order submitted successfully");
    return Accepted(new { OrderId = orderId });
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to submit order");
    return StatusCode(503, new { error = "Service unavailable" });
}
```

---

#### 3.2 Capture All Failure Paths to Database

**Action**: Create an `OrderAuditLog` table to capture all state transitions and errors:

**Add `Models/OrderAuditLog.cs`**:
```csharp
public class OrderAuditLog
{
    public int Id { get; set; }
    public string OrderId { get; set; } = null!;
    public string EventType { get; set; } = null!;  // "OrderSubmitted", "PaymentFailed", "Timeout", etc.
    public string State { get; set; } = null!;      // "Submitted", "Failed", etc.
    public string? ErrorMessage { get; set; }
    public string? StackTrace { get; set; }
    public DateTime Timestamp { get; set; }
}
```

**Create a saga event listener** to log all transitions:

```csharp
// In OrderService/Program.cs during MassTransit configuration
x.AddSagaStateMachine<OrderStateMachine, OrderState>()
    .EntityFrameworkRepository(r =>
    {
        // ... existing config
    })
    .EntityFrameworkOutbox(r => r.UseSqlite()); // Also enable outbox for reliability

// Add saga observers
x.AddSagaStateMachineObserver<OrderSagaObserver>();
```

**Create `OrderService/Observers/OrderSagaObserver.cs`**:
```csharp
public class OrderSagaObserver : ISagaObserver<OrderState>
{
    private readonly OrderDbContext _db;
    private readonly ILogger<OrderSagaObserver> _logger;

    public OrderSagaObserver(OrderDbContext db, ILogger<OrderSagaObserver> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task PreCreate(SagaCreationContext<OrderState> context)
    {
        _logger.LogInformation("Creating saga for OrderId: {OrderId}", context.Saga.OrderId);
        await Task.CompletedTask;
    }

    public async Task PostCreate(SagaCreatedContext<OrderState> context)
    {
        var log = new OrderAuditLog
        {
            OrderId = context.Saga.OrderId,
            EventType = "SagaCreated",
            State = context.Saga.CurrentState.ToString(),
            Timestamp = DateTime.UtcNow
        };
        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync();
    }

    public async Task PreUpdate(SagaUpdateContext<OrderState> context)
    {
        _logger.LogInformation("Updating saga for OrderId: {OrderId}", context.Saga.OrderId);
        await Task.CompletedTask;
    }

    public async Task PostUpdate(SagaUpdatedContext<OrderState> context)
    {
        var log = new OrderAuditLog
        {
            OrderId = context.Saga.OrderId,
            EventType = context.Saga.CurrentState.ToString(),
            State = context.Saga.CurrentState.ToString(),
            Timestamp = DateTime.UtcNow
        };
        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync();
    }

    // ... implement other observer methods
}
```

---

#### 3.3 Health Checks Endpoint

**Action**: Add health checks for RabbitMQ connectivity:

**Create `OrderService/HealthChecks/RabbitMqHealthCheck.cs`**:
```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MassTransit;

public class RabbitMqHealthCheck : IHealthCheck
{
    private readonly IBusControl _bus;

    public RabbitMqHealthCheck(IBusControl bus)
    {
        _bus = bus;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _bus.GetProbeResult();
            return HealthCheckResult.Healthy("RabbitMQ connection is healthy");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ connection failed", ex);
        }
    }
}
```

**Register in `OrderService/Program.cs`**:
```csharp
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<OrderDbContext>()
    .AddCheck<RabbitMqHealthCheck>("rabbitmq");

// In MapEndpoints
app.MapHealthChecks("/health");
```

---

### 🟢 **Priority 4: Long-term Architectural Improvements**

#### 4.1 Implement Outbox Pattern
- Ensures saga events are durably stored before messaging
- Prevents lost events on consumer crash

#### 4.2 Add Distributed Tracing
- Already have OTEL configured but ensure exception details propagate
- Add custom baggage for order context

#### 4.3 Monitoring & Alerting
- Monitor DLQ depth (high count = systemic failure)
- Alert on saga timeouts
- Track retry rates by message type

#### 4.4 Dead Letter Queue Handler
- Implement dedicated consumer to capture and analyze DLQ messages
- Attempt re-processing with exponential backoff
- Notify operations team of persistent failures

---

## 4. Implementation Roadmap

### Phase 1 (Week 1): **Critical Gaps**
1. Add retry policies to InventoryService
2. Upgrade PaymentService retry to exponential backoff
3. Add saga timeout handling
4. Add API error handling

### Phase 2 (Week 2): **Resilience**
5. Add exception differentiation
6. Implement compensation (inventory release)
7. Add circuit breaker

### Phase 3 (Week 3): **Observability**
8. Structured logging with context
9. Audit log table and saga observer
10. Health checks endpoint

### Phase 4 (Ongoing): **Polish**
11. Outbox pattern
12. DLQ handler
13. Distributed tracing enhancements
14. Monitoring & alerting dashboards

---

## 5. Testing Recommendations

### Add These Test Cases

```csharp
// ECommerce.Tests/OrderSagaTimeoutTests.cs
[Fact]
public async Task Should_Fail_Order_After_Timeout()
{
    // Submit order, don't send inventory response
    // Wait for timeout
    // Assert saga transitioned to Failed with timeout reason
}

// ECommerce.Tests/PaymentRetryTests.cs
[Fact]
public async Task Should_Retry_Transient_Payment_Failure()
{
    // Send payment message that throws transient error
    // Verify it was retried N times
    // Assert final state is either success or DLQ
}

// ECommerce.Tests/CompensationTests.cs
[Fact]
public async Task Should_Release_Inventory_When_Payment_Fails()
{
    // Submit order, reserve inventory, fail payment
    // Assert ReleaseInventory command was published
}
```

---

## 6. Configuration Checklist

Before production deployment:

- [ ] All services have retry policies configured
- [ ] Saga has timeout configured (with value appropriate for business)
- [ ] Exception types differentiated (transient vs. permanent)
- [ ] Circuit breaker configured for downstream services
- [ ] Health checks deployed and monitored
- [ ] Structured logging with correlation IDs enabled
- [ ] Audit logging implemented and tested
- [ ] DLQ handling strategy documented
- [ ] Alert thresholds set (retry count, DLQ depth, timeout rate)
- [ ] Runbook created for operations team (DLQ investigation, manual compensation)

---

## Summary

| Gap | Severity | Impact | Effort | First Step |
|-----|----------|--------|--------|-----------|
| Missing retries (Inventory) | 🔴 Critical | Transient failures → permanent data loss | 2 hrs | Add `UseMessageRetry()` |
| No saga timeout | 🔴 Critical | Stuck orders, no user feedback | 3 hrs | Add `Schedule()` + timeout event |
| No API error handling | 🔴 Critical | Crashes on infrastructure failure | 1 hr | Add try-catch in controller |
| Missing compensation | 🟠 High | Orphaned inventory reservations | 4 hrs | Add `ReleaseInventory` message |
| No exception differentiation | 🟠 High | Unnecessary retries, slower failure | 3 hrs | Create custom exceptions |
| No circuit breaker | 🟠 High | Cascading failures, slow recovery | 2 hrs | Add `UseCircuitBreaker()` |
| Poor error observability | 🟡 Medium | Hard to debug issues | 6 hrs | Structured logging + audit table |
| No health checks | 🟡 Medium | Can't detect system-wide failures | 2 hrs | Add RabbitMQ health check |

---

**Next Step**: Start with Phase 1 (Priority 1) for immediate production readiness. I can help implement any of these recommendations.
