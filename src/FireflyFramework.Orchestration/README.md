# FireflyFramework.Orchestration

Saga, Workflow, and TCC orchestration engines with DAG execution,
compensation, signals, timers, dead-letter capture, and configurable
compensation policies. Mirrors `org.fireflyframework:firefly-common-domain`
orchestration.

## Saga

Annotation-driven distributed transaction. The engine runs `[SagaStep]`
methods in dependency order (topological sort over `DependsOn`); on
failure it walks back through completed steps and invokes the matching
`Compensate` method on each.

```csharp
using FireflyFramework.Orchestration.Saga;

[Saga("CheckoutSaga")]
public sealed class CheckoutSaga
{
    [SagaStep("reserve-inventory", Compensate = nameof(ReleaseInventory))]
    public Task ReserveInventoryAsync() => /* ... */;

    [SagaStep("charge-card",
              DependsOn  = new[] { "reserve-inventory" },
              Compensate = nameof(RefundCard))]
    public Task ChargeCardAsync() => /* ... */;

    public Task ReleaseInventoryAsync() => /* ... */;
    public Task RefundCardAsync()        => /* ... */;
}

var engine = new SagaEngine(logger);
var result = await engine.ExecuteAsync(new CheckoutSaga(), ct);
```

## Workflow

Long-running orchestrations with signals, timers, and durable
checkpoints. The engine runs `[WorkflowStep]` methods in declared order.
`[WaitForSignal]` blocks until an external caller publishes a signal;
`[WaitForTimer]` delays for a configurable duration.

```csharp
using FireflyFramework.Orchestration.Workflow;

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
var run    = engine.ExecuteAsync(new OrderApprovalWorkflow(), input: orderRequest, ct);

// Elsewhere, when the approval arrives:
await signals.SendAsync(correlationId, name: "approval", payload: "alice", ct);
```

## TCC (Try / Confirm / Cancel)

Coordinated multi-resource transactions with strong rollback guarantees.

```csharp
using FireflyFramework.Orchestration.Tcc;

[Tcc("FundsTransfer")]
public sealed class TransferCoordinator { }

[TccParticipant]
public sealed class DebitParticipant
{
    [TryMethod]     public Task<string> Try()                                  => /* reserve */;
    [ConfirmMethod] public Task          Confirm([FromTry] string reservationId) => /* commit */;
    [CancelMethod]  public Task          Cancel ([FromTry] string reservationId) => /* release */;
}

var engine  = new TccEngine(logger);
var result  = await engine.ExecuteAsync(new TransferCoordinator(), new object[] { debit, credit }, ct);
```

If any `Try` fails, the engine cancels every participant that completed
its `Try`. If all `Try` calls succeed, it runs `Confirm` on each.

## Public surface

### Engines

| Type                           | Purpose                                              |
|--------------------------------|------------------------------------------------------|
| `SagaEngine`                   | Topologically sorted saga executor with compensation |
| `WorkflowEngine`               | Step-driven workflow with checkpoints                |
| `TccEngine`                    | Two-phase Try/Confirm/Cancel coordinator             |
| `SignalService`                | External signal injection into workflows             |
| `TimerService`                 | Durable scheduled callbacks                          |

### Lifecycle

`ExecutionStatus` covers the full lifecycle: `Pending`, `Running`,
`Waiting`, `Suspended`, `Completed`, `Failed`, `Cancelled`, `TimedOut`,
`Trying`, `Confirming`, `Confirmed`, `Cancelling`, `Canceled`,
`Compensating`. `StepStatus` covers per-step state.

### Persistence

| Type                                | Purpose                                                |
|-------------------------------------|--------------------------------------------------------|
| `IExecutionPersistenceProvider`     | Pluggable storage for execution state                  |
| `InMemoryPersistenceProvider`       | Default in-process implementation                      |
| `OrchestrationExecutionContext`     | Carrier for `CorrelationId`, `Pattern`, status, custom data |

### Dead letter

| Type                          | Purpose                                                                |
|-------------------------------|------------------------------------------------------------------------|
| `IDeadLetterStore`            | Captures failed orchestration executions for replay or discard         |
| `InMemoryDeadLetterStore`     | Default in-process implementation                                      |
| `IDeadLetterReplayService`    | Replays a dead-lettered execution into the appropriate engine          |

### Compensation policies

| Type                              | Purpose                                                              |
|-----------------------------------|----------------------------------------------------------------------|
| `CompensationFailureAction`       | `Abort`, `Skip`, `Retry`, `DeadLetter`                               |
| `CompensationPolicy`              | Strategy + max retries + retry delay + continue-on-failure flag      |
| `CompensationPolicy.Default`      | Abort the rollback chain on first failure                            |
| `CompensationPolicy.SkipOnFailure` | Skip the failing step and continue                                  |
| `CompensationPolicy.RetryThenDeadLetter` | Retry up to 3 times, then dead-letter and continue            |
| `CompensationStepResult`          | Per-step outcome (success, attempts, error, duration)                |
| `CompensationReport`              | Whole-rollback report                                                |

## Dependencies

| Reference                              | Used for                                |
|----------------------------------------|-----------------------------------------|
| `FireflyFramework.Kernel`              | Base exceptions                         |
| `FireflyFramework.EventSourcing`       | Optional event-sourced persistence      |
| `Microsoft.Extensions.Logging.Abstractions` | Engine logging                     |

## Java mapping

| .NET                              | Java                                      |
|-----------------------------------|-------------------------------------------|
| `SagaEngine`                      | `SagaEngine`                              |
| `WorkflowEngine`                  | `WorkflowEngine`                          |
| `TccEngine`                       | `TccEngine`                               |
| `IDeadLetterStore`                | `DeadLetterStore` + `DeadLetterService`   |
| `CompensationPolicy`              | `CompensationPolicy`                      |
| `CompensationReport`              | `CompensationReport`                      |
