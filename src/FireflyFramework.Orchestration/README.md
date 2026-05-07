# FireflyFramework.Orchestration

## Overview

`FireflyFramework.Orchestration` is the **distributed-coordination tier** of the
Firefly framework. It bundles three orchestration engines — Saga, Workflow,
and TCC — behind a common execution-context, persistence, and dead-letter
model, and ships the supporting machinery that production deployments
need: recovery, scheduling, topology rendering, and a REST control plane.

It mirrors the Java `org.fireflyframework:firefly-common-domain`
orchestration module one-to-one. The annotation surface, status
machine, and engine semantics are identical; the only translations are
the .NET-idiomatic ones (`[Saga]` instead of `@Saga`,
`OrchestrationExecutionContext` instead of `OrchestrationExecutionContext`
with the same field names, async over `Future`).

The three engines are intentionally separate:

| Engine    | Use when…                                                                                                                      |
|-----------|--------------------------------------------------------------------------------------------------------------------------------|
| **Saga**  | You have a sequence (or DAG) of independent steps where each can be undone with a *compensating* action. Reservation flows, multi-service order processing, "best-effort consistency" pipelines. |
| **Workflow** | You need long-running orchestration with external signals, durable timers, queries, and explicit checkpoints. Approval flows, human-in-the-loop, multi-day onboarding. |
| **TCC**   | You need strong rollback guarantees across multiple resources with a Try / Confirm / Cancel handshake. Funds transfer, inventory + payment + shipping coordination, rental booking. |

A service can use several engines in the same process — they share
`IExecutionPersistenceProvider`, `IDeadLetterStore`, and
`OrchestrationExecutionContext` so observability and operational tools
work uniformly.

## Why a separate module?

The Java framework chose to keep orchestration *outside* `firefly-common-cqrs`
because the abstractions are different: CQRS is one-shot
request/response, orchestration is a multi-step state machine that may
sleep for hours. Mixing them dilutes the bus, complicates the
middleware pipeline, and forces every command handler to import saga
metadata.

The same separation is preserved on .NET. `FireflyFramework.Cqrs`
remains pure command/query; `FireflyFramework.Orchestration` is where
multi-step, durable, compensating, signal-driven flows live.

## Mental model

```
                      ┌─────────────────────────────────┐
                      │  OrchestrationExecutionContext  │
                      │  CorrelationId / Pattern /      │
                      │  Status / StepResults /         │
                      │  Variables / TccPhase           │
                      └──────────────┬──────────────────┘
                                     │ shared
        ┌────────────────────────────┼────────────────────────────┐
        │                            │                            │
   ┌────▼─────┐                ┌─────▼─────┐                ┌─────▼──────┐
   │  Saga    │                │  Workflow │                │   TCC      │
   │  Engine  │                │  Engine   │                │  Engine    │
   │          │                │           │                │            │
   │ DAG sort │                │ Sequential│                │  Try → all │
   │ + comp.  │                │ + signals │                │  Confirm / │
   │          │                │ + timers  │                │  Cancel    │
   └────┬─────┘                └─────┬─────┘                └─────┬──────┘
        │                            │                            │
        └────────────────────────────┼────────────────────────────┘
                                     │
              ┌──────────────────────┼──────────────────────┐
              │                      │                      │
         ┌────▼─────┐           ┌────▼─────┐           ┌────▼──────┐
         │ Persist. │           │ Recovery │           │ Dead-     │
         │ provider │           │ service  │           │ letter    │
         │          │           │ (cron)   │           │ store     │
         └──────────┘           └──────────┘           └───────────┘
```

The engines are stateless reflection-driven executors. The only mutable
state of a run lives in `OrchestrationExecutionContext`, which is what
the persistence provider stores and what recovery and dead-letter
operations re-hydrate.

## Saga

### Annotation surface

A saga is a class annotated `[Saga("name")]` with one or more methods
annotated `[SagaStep(id, …)]`. Each step optionally lists `DependsOn`
predecessors and a `Compensate` method name; on failure, the engine
unwinds completed steps in reverse and invokes their compensators.

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

    [SagaStep("book-shipping",
              DependsOn  = new[] { "charge-card" },
              Compensate = nameof(CancelShipping))]
    public Task BookShippingAsync() => /* ... */;

    public Task ReleaseInventoryAsync() => /* ... */;
    public Task RefundCardAsync()       => /* ... */;
    public Task CancelShippingAsync()   => /* ... */;
}

var engine = new SagaEngine(logger);
var result = await engine.ExecuteAsync(new CheckoutSaga(), ct);

if (!result.Success)
{
    logger.LogError(result.Error,
        "Checkout failed at correlation {Cid}, rolled back {Steps} steps",
        result.Context.CorrelationId,
        result.Context.StepResults.Values.Count(r => r.Status == StepStatus.Compensated));
}
```

### How execution proceeds

1. The engine reflects the type once and discovers all `[SagaStep]`
   methods.
2. A topological sort is performed over `DependsOn`. Cycles raise
   `InvalidOperationException` *before* the saga runs.
3. Steps run in topo order; each successful step is pushed onto a
   `Stack<(method, attribute)>`.
4. On the first failure, the engine pops that stack and invokes each
   step's `Compensate` method in reverse.
5. If a compensator throws, the step's `StepStatus` flips to
   `CompensationFailed` but the rollback continues — half-rolled-back
   sagas are logged loudly so an operator can intervene.

### Compensation policies

`CompensationPolicy` controls what happens *during* the rollback if a
compensator itself fails:

| Policy preset                         | Failure action | Retries | Continue on failure |
|---------------------------------------|----------------|---------|---------------------|
| `CompensationPolicy.Default`          | `Abort`        | 0       | no                  |
| `CompensationPolicy.SkipOnFailure`    | `Skip`         | 0       | yes                 |
| `CompensationPolicy.RetryThenDeadLetter` | `Retry`     | 3       | yes (after retry budget) |

Pick `Default` only when you know every compensator is idempotent and
you'd rather page a human than risk a partial rollback. Pick
`RetryThenDeadLetter` for production: it retries a compensator with
exponential backoff and, if it still fails, pushes the failed
compensation to the dead-letter store and continues unwinding earlier
steps so the partial rollback is at least auditable.

## Workflow

### Annotation surface

A workflow is a class annotated `[Workflow("id")]` with methods
annotated `[WorkflowStep("id")]`. Steps run **sequentially** in
ascending step id order. Each step may carry `[WaitForSignal]` (block
until an external publisher delivers the signal) and/or
`[WaitForTimer]` (sleep for a configurable duration before running).

```csharp
using FireflyFramework.Orchestration.Workflow;

[Workflow("OrderApproval", Version = "2", TimeoutMs = 86_400_000 * 7)]
public sealed class OrderApprovalWorkflow
{
    [WorkflowStep("01-submit")]
    public Task Submit(OrchestrationExecutionContext ctx) =>
        Task.FromResult(ctx.Variables["submittedAt"] = DateTimeOffset.UtcNow);

    [WorkflowStep("02-await-approval")]
    [WaitForSignal("approval", TimeoutMs = 86_400_000)] // 24h SLA
    public Task ContinueWithApproval(string approverId,
                                     OrchestrationExecutionContext ctx)
    {
        ctx.Variables["approver"] = approverId;
        return Task.CompletedTask;
    }

    [WorkflowStep("03-cool-off")]
    [WaitForTimer(durationMs: 60_000)]   // give the user a minute to revoke
    public Task CoolOff() => Task.CompletedTask;

    [WorkflowStep("04-fulfill")]
    public Task Fulfill() => /* ... */;

    [WorkflowQuery("status")]
    public string GetStatus(OrchestrationExecutionContext ctx) =>
        ctx.Status.ToString();
}
```

### How signals work

`SignalService` is an in-process exchange of
`TaskCompletionSource<object?>` keyed on
`{workflowId}:{correlationId}:{signalName}`. When a step is decorated
`[WaitForSignal("approval")]`:

1. The engine computes the key and registers a TCS.
2. Every step before the wait point runs without any external
   coordination.
3. When the engine reaches the wait, it `await`s the TCS, optionally
   with a `TimeoutMs`.
4. An out-of-band caller publishes via `WorkflowEngine.SendSignal(...)`
   or `signals.Publish(key, payload)`. The TCS resolves and the step
   continues; the published payload is type-matched to a step
   parameter.

The default `SignalService` is **in-process only**. For multi-instance
deployments, swap it for an implementation backed by Redis pub/sub, a
database row with notify, or a Kafka topic.

### How timers work

`TimerService` defaults to `Task.Delay(...)`. A workflow that's mid-`[WaitForTimer]`
when its host crashes will *lose* the wait. Override `TimerService` to
persist scheduled wake-ups and re-queue them on startup if you need
durable timers.

### Lifecycle services

| Type                          | Purpose                                                                |
|-------------------------------|------------------------------------------------------------------------|
| `WorkflowRegistry`            | Registers `[Workflow]` types so they can be started by id              |
| `WorkflowLifecycleService`    | Cancel / suspend / resume an in-flight workflow                        |
| `WorkflowQueryService`        | Invokes `[WorkflowQuery]` methods to inspect a running workflow's state |
| `SearchAttributeProjection`   | Projects workflow variables into a searchable index                    |

## TCC (Try / Confirm / Cancel)

### Annotation surface

A coordinator is `[Tcc("name")]`; each participant is `[TccParticipant]`
with a `[TryMethod]`, `[ConfirmMethod]`, and `[CancelMethod]`. The Try
method can return a value (typically a reservation id); `[FromTry]`
parameters on Confirm and Cancel receive that return value.

```csharp
using FireflyFramework.Orchestration.Tcc;

[Tcc("FundsTransfer", TimeoutMs = 30_000, MaxRetries = 3)]
public sealed class TransferCoordinator { }

[TccParticipant]
public sealed class DebitAccountParticipant
{
    [TryMethod(TimeoutMs = 5_000)]
    public Task<string> Try() =>
        /* reserve the debit; return reservation id */
        Task.FromResult(Guid.NewGuid().ToString());

    [ConfirmMethod(TimeoutMs = 5_000)]
    public Task Confirm([FromTry] string reservationId) =>
        /* irrevocable: commit the debit                                  */;

    [CancelMethod(TimeoutMs = 5_000)]
    public Task Cancel([FromTry] string reservationId) =>
        /* idempotent: release the reservation                            */;
}
```

### How execution proceeds

1. **Try phase.** The engine calls Try on every participant in turn.
   - If all succeed, every Try return value is captured in a dictionary
     keyed on the participant.
   - If any Try throws, the engine immediately runs Cancel on every
     participant whose Try succeeded, then returns
     `TccResult(success: false)`.
2. **Confirm phase.** The engine calls Confirm on every participant,
   passing the captured Try return value to `[FromTry]` parameters.
   - If all Confirms succeed, the result is `success: true`.
   - If a Confirm throws, the engine **does not** roll back — Confirms
     are by definition irrevocable. The error is logged loudly and the
     execution is marked `Failed` for an operator to triage.

The Try-then-Confirm protocol assumes participants treat Try as
*reservation* (cheap, reversible) and Confirm as *commit*
(idempotent, must succeed once Try succeeded). That contract is the
whole reason TCC is preferred over a saga when you absolutely cannot
afford a half-applied state.

## Public surface

### Engines

| Type                           | Purpose                                              |
|--------------------------------|------------------------------------------------------|
| `SagaEngine`                   | Topologically sorted saga executor with compensation |
| `WorkflowEngine`               | Step-driven workflow with checkpoints, signals, timers |
| `TccEngine`                    | Two-phase Try / Confirm / Cancel coordinator         |
| `SignalService`                | External signal injection into workflows             |
| `TimerService`                 | Durable scheduled callbacks (override for persistence) |

### Lifecycle enums

`ExecutionStatus` covers the full lifecycle (one machine, three engines):

| Value           | Engines              | Meaning                                         |
|-----------------|----------------------|-------------------------------------------------|
| `Pending`       | all                  | Created, not yet running                        |
| `Running`       | saga / workflow      | Executing a step                                |
| `Waiting`       | workflow             | Blocked on a `[WaitForSignal]`                  |
| `Suspended`     | workflow             | Manually suspended via `WorkflowLifecycleService` |
| `Completed`     | all                  | Terminal: every step succeeded                  |
| `Failed`        | all                  | Terminal: a step or Confirm threw               |
| `Cancelled`     | all                  | Terminal: cancellation token tripped            |
| `TimedOut`      | all                  | Terminal: exceeded `TimeoutMs`                  |
| `Trying`        | tcc                  | In Try phase                                    |
| `Confirming`    | tcc                  | In Confirm phase                                |
| `Confirmed`     | tcc                  | Terminal: every Confirm succeeded               |
| `Cancelling`    | tcc                  | A Try failed; running Cancel on completed Tries |
| `Canceled`      | tcc                  | Terminal: TCC ended via cancel-after-try        |
| `Compensating`  | saga                 | Terminal: rolled back due to a step failure     |

`StepStatus` is the per-step machine: `NotStarted`, `Running`,
`Completed`, `Failed`, `Skipped`, `Retrying`, `Compensated`,
`CompensationFailed`.

`ExecutionPattern` discriminates the three engines (`Workflow`, `Saga`,
`Tcc`); `TriggerMode` is `Sync` or `Async`.

### Persistence

| Type                                | Purpose                                                |
|-------------------------------------|--------------------------------------------------------|
| `IExecutionPersistenceProvider`     | Pluggable storage for execution state                  |
| `InMemoryPersistenceProvider`       | Default in-process implementation                      |
| `OrchestrationExecutionContext`     | Carrier for `CorrelationId`, `Pattern`, status, variables |

The persistence provider is the entire integration surface for durable
orchestration. To plug a database, implement `SaveAsync`,
`FindByIdAsync`, `UpdateStatusAsync`, the `Find*` enumerables,
`CleanupAsync`, `FindStaleAsync`, and `IsHealthyAsync`. The framework
ships only the in-memory implementation — production deployments are
expected to back this with EF Core, Dapper, or a NoSQL store.

### Recovery

`RecoveryService` is the operational glue between persistence and the
engines. It exposes two operations:

| Method                       | Purpose                                                            |
|------------------------------|--------------------------------------------------------------------|
| `FindStaleAsync(ct)`         | Streams every in-flight execution older than `StaleThreshold` (default 5 min). |
| `CleanupCompletedAsync(olderThan, ct)` | Removes terminal executions older than the given window. |

The recovery loop itself isn't started automatically — you compose it
with the `OrchestrationScheduler` (or any `IHostedService`) so the
framework doesn't impose a fixed cadence:

```csharp
scheduler.ScheduleWithFixedDelay(
    "orchestration-recovery",
    async ct =>
    {
        await foreach (var stale in recovery.FindStaleAsync(ct))
        {
            await coordinator.ResumeAsync(stale.CorrelationId, ct);
        }
    },
    initialDelay: TimeSpan.FromSeconds(30),
    delay:        TimeSpan.FromMinutes(1));

scheduler.ScheduleWithCron(
    "orchestration-cleanup",
    ct => recovery.CleanupCompletedAsync(TimeSpan.FromDays(30), ct),
    cronExpression: "0 0 3 * * *",   // 03:00 UTC daily
    timeZone:       TimeZoneInfo.Utc);
```

### Dead-letter

| Type                          | Purpose                                                                |
|-------------------------------|------------------------------------------------------------------------|
| `IDeadLetterStore`            | Captures failed orchestration executions for replay or discard         |
| `DeadLetterEntry`             | Immutable record of a failed execution: id, correlation, pattern, reason, stack, state |
| `InMemoryDeadLetterStore`     | Default in-process implementation                                      |
| `IDeadLetterReplayService`    | Replays a dead-lettered execution into the appropriate engine          |

Compensation failures (when policy is `DeadLetter`) and Confirm-phase
failures both land here. The control plane exposes them at
`/orchestration/dead-letter` so an operator can inspect, retry, or
discard.

### Compensation policies

| Type                              | Purpose                                                              |
|-----------------------------------|----------------------------------------------------------------------|
| `CompensationFailureAction`       | `Abort`, `Skip`, `Retry`, `DeadLetter`                               |
| `CompensationPolicy`              | Strategy + max retries + retry delay + continue-on-failure flag      |
| `CompensationStepResult`          | Per-step outcome (success, attempts, error, duration)                |
| `CompensationReport`              | Whole-rollback report                                                |

### Topology

`TopologyBuilder` produces an immutable `TopologyGraph` (nodes + edges).
The graph has helpers for topological sort and cycle detection. The
companion `TopologyGraphGenerator` renders the graph as Graphviz / Mermaid
for documentation and ops dashboards.

```csharp
var graph = new TopologyBuilder()
    .AddStep("reserve-inventory", "Reserve inventory")
    .AddStep("charge-card",       "Charge card")
    .AddStep("book-shipping",     "Book shipping")
    .AddDependency("reserve-inventory", "charge-card")
    .AddDependency("charge-card",       "book-shipping")
    .Build();

var mermaid = TopologyGraphGenerator.ToMermaid(graph);
```

### Scheduling

`IOrchestrationScheduler` exposes three scheduling primitives:

| Method                       | Cadence                                                            |
|------------------------------|--------------------------------------------------------------------|
| `ScheduleAtFixedRate(...)`   | Fires every `period` regardless of how long the previous run took  |
| `ScheduleWithFixedDelay(...)`| Fires after `delay` *between* the end of one run and the start of the next |
| `ScheduleWithCron(...)`      | Fires on a Quartz-flavoured cron expression (5- or 6-field)        |

Cron parsing is via the `Cronos` package (same expression dialect
Spring's `CronExpression` understands). Per-task exceptions are caught
and logged — a single bad invocation never tears down the whole
scheduler.

### REST control plane

| Controller                | Routes                                                            |
|---------------------------|-------------------------------------------------------------------|
| `OrchestrationController` | `GET /orchestration/executions`, `GET /executions/{id}`, `POST /executions/{id}/cancel` |
| `WorkflowController`      | `POST /workflows/{id}/signals/{name}`, `POST /workflows/{id}/queries/{name}` |
| `DeadLetterController`    | `GET /orchestration/dead-letter`, `POST /dead-letter/{id}/replay`, `DELETE /dead-letter/{id}` |

These are the same routes the Java line exposes, so existing Grafana
dashboards, runbooks, and replay scripts work unchanged.

## Configuration

This module reads no configuration directly. Wiring lives in the
hosting service:

```csharp
services.AddSingleton<SignalService>();
services.AddSingleton<TimerService>();
services.AddSingleton<SagaEngine>();
services.AddSingleton<WorkflowEngine>();
services.AddSingleton<TccEngine>();
services.AddSingleton<IExecutionPersistenceProvider, EfCoreExecutionPersistenceProvider>();
services.AddSingleton<IDeadLetterStore, EfCoreDeadLetterStore>();
services.AddSingleton<RecoveryService>();
services.AddSingleton<IOrchestrationScheduler, OrchestrationScheduler>();
services.AddHostedService<OrchestrationRecoveryHost>();
```

For the typical case, the `FireflyFramework.Starter.Application` starter
registers the in-memory provider + scheduler so a service is runnable
out of the box, and you swap to the EF Core provider when you wire
durability.

## Common patterns

### Saga with compensation policy

```csharp
var policy = CompensationPolicy.RetryThenDeadLetter;   // production default
var engine = new SagaEngine(logger);
var result = await engine.ExecuteAsync(saga, ct);

if (!result.Success)
{
    var compensated = result.Context.StepResults.Values
        .Where(r => r.Status == StepStatus.Compensated)
        .Select(r => r.StepId)
        .ToArray();
    var failedToCompensate = result.Context.StepResults.Values
        .Where(r => r.Status == StepStatus.CompensationFailed)
        .Select(r => r.StepId)
        .ToArray();

    if (failedToCompensate.Length > 0)
    {
        // Half-rolled-back saga — page on-call
        await alerts.PagedRollbackFailureAsync(result.Context.CorrelationId,
                                               failedToCompensate);
    }
}
```

### Workflow with external signal

```csharp
var run = engine.ExecuteAsync(new OrderApprovalWorkflow(), input: orderRequest, ct);

// Elsewhere — say, in an HTTP handler that receives the approver's click:
[HttpPost("/orders/{id}/approve")]
public async Task<IActionResult> Approve(string id, [FromBody] ApprovalDto dto)
{
    var ok = engine.SendSignal(
        workflowId:   "OrderApproval",
        correlationId: id,
        signalName:   "approval",
        payload:      dto.ApproverId);
    return ok ? Ok() : Conflict("No workflow waiting for this approval.");
}
```

### TCC with external participants

```csharp
var participants = new object[]
{
    debitAccountParticipant,
    creditAccountParticipant,
    auditLedgerParticipant,
};

var coordinator = new TransferCoordinator();
var result = await tccEngine.ExecuteAsync(coordinator, participants, ct);

if (!result.Success)
{
    if (result.Context.Status == ExecutionStatus.Failed
        && result.Context.TccPhase == TccPhase.Confirm)
    {
        // Confirm failure — irrevocable; an operator must reconcile manually.
        await alerts.PagedTccConfirmFailureAsync(result.Context.CorrelationId);
    }
    else
    {
        // Try-time failure with successful Cancel on all completed Tries —
        // safe to retry.
        logger.LogInformation("TCC {Id} cancelled cleanly; retry-safe", result.Context.CorrelationId);
    }
}
```

### Periodic recovery + cleanup

```csharp
// Hosted service that wires recovery + cleanup into the scheduler.
public sealed class OrchestrationRecoveryHost(
    IOrchestrationScheduler scheduler,
    RecoveryService recovery,
    ICoordinator coordinator,
    ILogger<OrchestrationRecoveryHost> log) : IHostedService
{
    public Task StartAsync(CancellationToken ct)
    {
        scheduler.ScheduleWithFixedDelay("orchestration-recovery",
            async token =>
            {
                await foreach (var stale in recovery.FindStaleAsync(token))
                {
                    log.LogWarning("Recovering stale {Pattern} {Id}",
                        stale.Pattern, stale.CorrelationId);
                    await coordinator.ResumeAsync(stale.CorrelationId, token);
                }
            },
            initialDelay: TimeSpan.FromSeconds(30),
            delay: TimeSpan.FromMinutes(1));

        scheduler.ScheduleWithCron("orchestration-cleanup",
            ct2 => recovery.CleanupCompletedAsync(TimeSpan.FromDays(30), ct2),
            cronExpression: "0 0 3 * * *",
            timeZone: TimeZoneInfo.Utc);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

## Pitfalls and gotchas

- **Saga step methods must be idempotent.** A retried step may have
  already completed in a previous attempt; design `ReserveInventory`,
  `ChargeCard`, and similar to detect prior reservation by id and
  short-circuit. The framework helps by passing a stable
  `CorrelationId` in the context, but the *guarantee* lives in your
  code.
- **Compensators must be idempotent too.** With `CompensationPolicy.Retry`
  the same compensator can run multiple times — releasing a
  reservation twice must not throw.
- **Cycles are detected, missing dependencies are not.** If a step's
  `DependsOn` mentions a step id that doesn't exist, that dependency
  is silently dropped. Either rely on a CI test that pins the topo
  graph, or call `TopologyBuilder.Build()` over the same ids and let
  it throw.
- **Workflow step order is by step id.** Methods are sorted
  lexicographically on `[WorkflowStep("…")]`. Use a numeric prefix
  (`"01-submit"`, `"02-await-approval"`, …) so insertions don't
  silently reorder downstream steps.
- **Default `SignalService` is in-process.** A workflow waiting on a
  signal in instance A will not be woken by a publish from instance B.
  Swap to a distributed implementation before scaling out.
- **Default `TimerService` is in-process.** A workflow that's mid-`[WaitForTimer]`
  when the host crashes loses the wait. Override for durability if
  your timers exceed pod lifetime.
- **TCC Confirm failures are unrecoverable in-band.** The framework
  refuses to roll back a successful Try after a Confirm failure
  because it would violate the protocol. Instead, the run lands in
  `Failed`, the dead-letter store captures the context, and an
  operator reconciles. Plan for this at design time — don't put
  irreversible side effects (send email, send funds) into Confirm
  unless the upstream system itself is bullet-proof.
- **`CleanupCompletedAsync(olderThan)` removes records, not state.**
  The audit data goes with them. If you need long-term forensics,
  archive to a cold table before invoking cleanup.
- **Cron expressions support both 5-field and 6-field formats.** The
  6-field form has seconds in position 0; the parser detects which is
  which by counting whitespace-separated tokens. `0 */5 * * * *` is
  every 5 minutes (with seconds); `*/5 * * * *` is also every 5
  minutes (without seconds). Both work — but mixing them across
  scheduled tasks invites confusion.

## Internals (for the curious)

- The reflection-based engine uses a single dictionary lookup per
  invocation: methods are reflected once when the engine first sees a
  saga / workflow type, then cached. The cost is paid at warm-up, not
  per-execution.
- Compensation runs are best-effort by design — `CompensationFailed`
  *does not* halt subsequent compensators (unless the policy says
  `Abort`). The rationale is that releasing the second reservation is
  still better than releasing nothing because the first reservation's
  release threw.
- `OrchestrationExecutionContext.Variables` is a plain
  `Dictionary<string, object?>` rather than `ConcurrentDictionary` —
  steps run sequentially within a single execution, so the
  thread-safety overhead is unnecessary. Cross-execution access (from
  the REST control plane, from recovery) goes through the persistence
  provider, which is responsible for its own concurrency.
- `OrchestrationScheduler` uses `Task.Delay` rather than
  `System.Threading.Timer` because the latter loses cancellation
  ergonomics across long-running async work. The trade-off: idle
  schedulers consume one suspended `Task.Delay` per task, but the
  scheduler is meant for hundreds of tasks, not millions.

## Dependencies

| Reference                              | Used for                                                  |
|----------------------------------------|-----------------------------------------------------------|
| `FireflyFramework.Kernel`              | Base exceptions, `OperationResult<T>`, clock              |
| `FireflyFramework.EventSourcing`       | Optional event-sourced persistence provider               |
| `Cronos`                               | Quartz-flavoured cron expression parsing                  |
| `Microsoft.Extensions.Logging.Abstractions` | Engine logging                                       |

## Java mapping

| .NET                              | Java                                      |
|-----------------------------------|-------------------------------------------|
| `SagaEngine`                      | `SagaEngine`                              |
| `WorkflowEngine`                  | `WorkflowEngine`                          |
| `TccEngine`                       | `TccEngine`                               |
| `SignalService`                   | `SignalService`                           |
| `TimerService`                    | `TimerService`                            |
| `RecoveryService`                 | `RecoveryService`                         |
| `OrchestrationScheduler`         | `OrchestrationScheduler`                  |
| `IDeadLetterStore`                | `DeadLetterStore` + `DeadLetterService`   |
| `CompensationPolicy`              | `CompensationPolicy`                      |
| `CompensationReport`              | `CompensationReport`                      |
| `TopologyBuilder` / `TopologyGraph` | `TopologyBuilder` / `TopologyGraph`     |
| `OrchestrationExecutionContext`   | `OrchestrationExecutionContext`           |
| `IExecutionPersistenceProvider`   | `ExecutionPersistenceProvider`            |
| `[Saga]` / `[SagaStep]`           | `@Saga` / `@SagaStep`                     |
| `[Workflow]` / `[WorkflowStep]`   | `@Workflow` / `@WorkflowStep`             |
| `[Tcc]` / `[TccParticipant]` / `[TryMethod]` / `[ConfirmMethod]` / `[CancelMethod]` / `[FromTry]` | identical Java annotations |
