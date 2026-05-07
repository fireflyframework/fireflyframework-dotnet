# FireflyFramework.Orchestration

Saga, Workflow and TCC orchestration engines with DAG execution, compensation, signals, timers and pluggable persistence. Mirrors `fireflyframework-orchestration`.

## Saga

Annotation-driven distributed transactions. The engine runs `[SagaStep]` methods in dependency order (topological sort over `DependsOn`); on failure it walks back through completed steps and invokes the matching `Compensate` method on each.

```csharp
[Saga("CheckoutSaga")]
public sealed class CheckoutSaga
{
    [SagaStep("reserve-inventory", Compensate = nameof(ReleaseInventory))]
    public Task ReserveInventoryAsync() => /* ... */;

    [SagaStep("charge-card", DependsOn = new[] { "reserve-inventory" }, Compensate = nameof(RefundCard))]
    public Task ChargeCardAsync() => /* ... */;

    public Task ReleaseInventoryAsync() => /* ... */;
    public Task RefundCardAsync() => /* ... */;
}

var engine = new SagaEngine(logger);
var result = await engine.ExecuteAsync(new CheckoutSaga());
```

## Workflow

Long-running orchestrations with signals, timers and child workflows. The engine runs `[WorkflowStep]` methods in declared order; `[WaitForSignal("name")]` blocks the step until an external caller publishes the signal; `[WaitForTimer(durationMs: …)]` delays.

```csharp
[Workflow("OrderApproval")]
public sealed class OrderApprovalWorkflow
{
    [WorkflowStep("01-submit")]
    public Task Submit() => /* ... */;

    [WorkflowStep("02-await-approval")]
    [WaitForSignal("approval", TimeoutMs = 86_400_000)]
    public Task ContinueWithApproval(string approverId) => /* record approval */;

    [WorkflowStep("03-fulfill")]
    public Task Fulfill() => /* ... */;
}

var engine = new WorkflowEngine(signals, timers, logger);
var run = engine.ExecuteAsync(new OrderApprovalWorkflow(), input: orderRequest);
// elsewhere in the app:
engine.SendSignal(workflowId: "OrderApproval", correlationId: ctx.CorrelationId, "approval", payload: "alice");
```

## TCC (Try-Confirm-Cancel)

Coordinated multi-resource transactions with strong rollback guarantees:

```csharp
[Tcc("FundsTransfer")]
public sealed class TransferCoordinator { }

[TccParticipant]
public sealed class DebitParticipant
{
    [TryMethod]    public Task<string> Try() => /* reserve */;
    [ConfirmMethod] public Task Confirm([FromTry] string reservationId) => /* commit */;
    [CancelMethod]  public Task Cancel([FromTry] string reservationId) => /* release */;
}

var engine = new TccEngine(logger);
var result = await engine.ExecuteAsync(new TransferCoordinator(), new object[] { debit, credit });
```

If any `Try` fails, the engine cancels all participants that completed `Try`. If all `Try` succeed, it runs `Confirm` on each.

## Pluggable persistence

`IExecutionPersistenceProvider` lets you swap the in-memory store for Redis or EF Core. The `InMemoryPersistenceProvider` is the default.

## Status enums

`ExecutionStatus` covers the full lifecycle (`Pending`, `Running`, `Waiting`, `Suspended`, `Completed`, `Failed`, `Cancelled`, `TimedOut`, `Trying`, `Confirming`, `Confirmed`, `Cancelling`, `Canceled`, `Compensating`). `StepStatus` covers per-step state.
