# FireflyFramework.Starter.Domain

## Overview

`FireflyFramework.Starter.Domain` is the **domain-tier meta-package** of
the Firefly Framework for .NET, designed for services that own
event-sourced aggregates. It composes `Starter.Core` with
`FireflyFramework.EventSourcing` and registers a default in-memory
`IEventStore`, giving a service everything it needs to append, replay,
and project domain events without any persistence configuration. When
the time comes to persist, the consumer swaps in the EF Core event store
implementation with a single line.

The Java equivalent is `org.fireflyframework:firefly-starter-domain`,
which composes `firefly-starter-core` with `firefly-event-sourcing` and
its R2DBC-backed event store. The .NET edition mirrors the composition
but defaults to the **in-memory event store** so a domain service can
boot with zero infrastructure for development, testing, and scratch
benchmarks. Production deployments override the registration with
`AddEfCoreEventStore<TDbContext>()` from
`FireflyFramework.EventSourcing.Store.EfCore`.

The reason this is a separate starter rather than an option flag on
`Starter.Core` is dependency hygiene: services that do not need event
sourcing should not pull `FireflyFramework.EventSourcing` (and its
JSON-serialisation, snapshotting, projection, and tenancy types) into
their compile-time graph. By making the domain tier opt-in, a typical
read-only or stateless service stays slim.

## When to use this module

Reach for `Starter.Domain` when:

- The service models a **rich domain aggregate** that emits events as
  the source of truth — for example, an `Order`, `Account`, `Loan`, or
  `Transaction` aggregate with optimistic concurrency requirements.
- You need **append-only history** for audit, replay, or temporal
  queries. Event sourcing makes "what did this aggregate look like at
  3pm yesterday?" a one-line replay.
- You will run **projections** (read models) off a global event stream
  using `IAsyncEnumerable<StoredEventEnvelope>`.
- You want to add an **outbox** for at-least-once event publishing
  alongside the database commit.

Prefer a different starter when:

- The service is a thin adapter over an existing relational schema
  without an event-sourced aggregate model → `Starter.Data`.
- The service hosts CQRS handlers but no aggregates → `Starter.Application`
  or `Starter.Core`.

## Mental model

```
              ┌─────────────────────────────────────┐
              │  AddFireflyDomain(...)              │
              │  ┌───────────────────────────────┐  │
              │  │  AddFireflyCore(...)          │  │
              │  └───────────────────────────────┘  │
              │  + IEventStore (InMemoryEventStore) │
              └─────────────────────────────────────┘
```

What the consumer is still expected to register:

| Concern                            | Registration                                               |
|------------------------------------|------------------------------------------------------------|
| Persistent event store             | `services.AddEfCoreEventStore<TDbContext>()` (replaces in-memory) |
| Aggregate roots                    | Per-aggregate handlers / repositories                      |
| Projections                        | `services.AddHostedService<MyProjector>()`                 |
| Snapshots                          | `services.AddSingleton<ISnapshotStore, ...>()`             |
| Outbox                             | `services.AddSingleton<IOutboxDispatcher, ...>()`          |
| IDP / orchestration                | Consumer-specific, same as `Starter.Application`           |

## Quick start

```csharp
using FireflyFramework.Starter.Domain;
using FireflyFramework.Web.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFireflyDomain(
    builder.Configuration,
    serviceName:    "orders-service",
    serviceVersion: "1.0.0",
    cqrsAssemblies: new[] { typeof(Program).Assembly });

var app = builder.Build();
app.UseFireflyWeb();
app.MapControllers();
await app.RunAsync();
```

The application now has an `IEventStore` resolved from DI. To test, the
in-memory implementation suffices; to deploy, swap it for the EF Core
adapter (see *Common patterns* below).

## Public surface

```csharp
namespace FireflyFramework.Starter.Domain;

public static class FireflyDomainExtensions
{
    public static IServiceCollection AddFireflyDomain(
        this IServiceCollection services,
        IConfiguration config,
        string serviceName,
        string serviceVersion = "1.0.0",
        params System.Reflection.Assembly[] cqrsAssemblies);
}
```

| Parameter        | Required | Purpose                                                                   |
|------------------|----------|---------------------------------------------------------------------------|
| `services`       | yes      | The DI container.                                                         |
| `config`         | yes      | The `IConfiguration` from which `Firefly:*` sections are bound.           |
| `serviceName`    | yes      | OpenTelemetry `service.name` resource attribute. Used in the banner.      |
| `serviceVersion` | no       | OpenTelemetry `service.version` attribute. Defaults to `"1.0.0"`.         |
| `cqrsAssemblies` | no       | Assemblies scanned for `ICommandHandler<,>` / `IQueryHandler<,>`.         |

After the call, one extra contract is resolvable:

| Service        | Default implementation | Lifetime  | Source                                   |
|----------------|------------------------|-----------|------------------------------------------|
| `IEventStore`  | `InMemoryEventStore`   | Singleton | `FireflyFramework.EventSourcing.Store`   |

The registration uses `TryAddSingleton`, so registering an alternative
store **before** calling `AddFireflyDomain` keeps the consumer's choice.

## Configuration

`Starter.Domain` adds no new configuration sections of its own. The
`InMemoryEventStore` requires zero configuration. When you switch to
`EfCoreEventStore`, the connection string lives under whatever section
your `DbContext` reads from — by convention,
`Firefly:Data:ConnectionString`.

All `Firefly:*` sections inherited from `Starter.Core` apply unchanged.

## Common patterns

### 1. Switching to the EF Core event store

```csharp
using FireflyFramework.EventSourcing.Store;
using FireflyFramework.EventSourcing.Store.EfCore;

builder.Services.AddDbContextFactory<EventStoreDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration["Firefly:Data:ConnectionString"]));

builder.Services.AddSingleton<IEventStore>(sp => new EfCoreEventStore(
    sp.GetRequiredService<IDbContextFactory<EventStoreDbContext>>(),
    knownEventTypes: new[] { typeof(OrderPlaced), typeof(OrderShipped) }));
```

`AddSingleton` (not `TryAddSingleton`) explicitly **replaces** the
default. Because the consumer's registration runs after the starter's
`TryAdd*`, the last registration wins.

### 2. Appending events with optimistic concurrency

```csharp
public sealed class OrderAggregate
{
    private readonly IEventStore _store;
    private readonly Guid _id;
    private long _version = -1;
    private readonly List<IDomainEvent> _pending = new();

    public OrderAggregate(IEventStore store, Guid id) { _store = store; _id = id; }

    public void Place(string sku, int qty, decimal unit) =>
        _pending.Add(new OrderPlaced(_id, sku, qty, unit, DateTimeOffset.UtcNow));

    public async Task SaveAsync(CancellationToken ct)
    {
        await _store.AppendEventsAsync(
            _id, "Order",
            _pending,
            expectedVersion: _version,
            metadata: new() { ["correlationId"] = Guid.NewGuid().ToString() },
            ct: ct);
        _version += _pending.Count;
        _pending.Clear();
    }
}
```

If two concurrent commits race, the loser receives a
`ConcurrencyException` (which inherits from `FireflyException` with
`ErrorCode = "ES_CONCURRENCY_VIOLATION"`).

### 3. Replaying an aggregate from history

```csharp
var stream = await store.LoadEventStreamAsync(orderId, "Order", ct: ct);
var order  = OrderAggregate.Empty(orderId);
foreach (var e in stream.Events) order.Apply(e);
```

`stream.Version` tells you the current aggregate version; pass it as
`expectedVersion` on the next append.

### 4. Building a projection

```csharp
public sealed class OrderProjector(IEventStore store) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var envelope in store.StreamAllEventsFromAsync(_lastSeq, ct))
        {
            // Update the read model
            _lastSeq = envelope.GlobalSequence;
        }
    }
}
```

`InMemoryEventStore.StreamAllEventsFromAsync` snapshots the global log
on enumeration start so projection consumers do not block writers.

## Pitfalls and gotchas

- **The default store is in-memory.** If you forget to swap in
  `EfCoreEventStore` in production, the application boots and serves
  traffic — but every event is lost on restart. Add a CI check that
  verifies the production composition root replaces `IEventStore`.
- **`expectedVersion` is `-1` for new aggregates.** A first append must
  pass `-1`; passing `0` raises `ConcurrencyException` because the
  stored version of a non-existent aggregate is also `-1`.
- **Events are JSON-serialised by default.** `InMemoryEventStore` calls
  `JsonSerializer.Serialize(@event, @event.GetType())`. Make event
  records public, immutable, and avoid `object` properties — round-trip
  through JSON drops the runtime type.
- **`StreamAllEventsAsync` snapshots on enumeration start.** Events
  appended after the consumer begins iterating are not visible until
  the next call. For a live tail, schedule periodic polling with
  `StreamAllEventsFromAsync(lastSeq)`.
- **Aggregate identity is `(Guid id, string aggregateType)`, not `Guid`
  alone.** Two aggregates of different types may share an id without
  conflict; pass the aggregate type consistently.
- **`ConcurrencyException` extends `FireflyException`.** It is a
  business condition, not a bug — your command handler should catch it,
  re-load the aggregate, and decide whether to retry, merge, or fail.
- **Snapshots and outbox are not registered automatically.** Even with
  `AddFireflyDomain`, you must wire `ISnapshotStore` and the outbox
  dispatcher yourself if you need them. The infrastructure types are
  available transitively.

## Internals (for the curious)

`AddFireflyDomain` is four lines:

```csharp
public static IServiceCollection AddFireflyDomain(...)
{
    FireflyBanner.Print(typeof(FireflyDomainExtensions).Assembly, serviceName, serviceVersion);
    services.AddFireflyCore(config, serviceName, serviceVersion, cqrsAssemblies);
    services.TryAddSingleton<IEventStore, InMemoryEventStore>();
    return services;
}
```

`InMemoryEventStore` keeps two structures behind a single lock:

- `Dictionary<(Guid, string), List<(StoredEventEnvelope, IDomainEvent)>>`
  for per-aggregate streams. The list index is the aggregate version.
- `List<StoredEventEnvelope>` for global ordering, used by the
  `StreamAllEventsAsync` projections feed.

`AppendEventsAsync` increments two counters: a global `_globalSeq`
shared across all aggregates and a per-aggregate `current` derived
from the list length. The combination of `(GlobalSequence,
AggregateVersion)` uniquely identifies every stored event.

The lock is coarse-grained — fine for tests and small production
workloads, but not the right shape for high throughput. For that
scenario, use the EF Core implementation, which leverages PostgreSQL's
unique-index check on `(aggregate_id, aggregate_type, version)` for
optimistic concurrency without process-wide locking.

The banner emitted by the domain starter reads `:: firefly-domain ::`.
Because `FireflyBanner._printed` is a process-wide latch, calling
`AddFireflyCore` (which also tries to print) afterwards is a no-op, so
the consumer sees a single domain banner.

## Dependencies

| Reference                              | Why                                                                                |
|----------------------------------------|------------------------------------------------------------------------------------|
| `FireflyFramework.Starter.Core`        | All of the infrastructure tier                                                     |
| `FireflyFramework.EventSourcing`       | `IEventStore`, `InMemoryEventStore`, snapshots, outbox, projection, upcasting, tenancy |

The package also embeds `Resources/banner.txt` containing the
`firefly-domain` ASCII tag printed at startup.

## Java mapping

| .NET                            | Java                                                                  |
|---------------------------------|-----------------------------------------------------------------------|
| `AddFireflyDomain`              | `org.fireflyframework:firefly-starter-domain`                         |
| `IEventStore`                   | `EventStore`                                                          |
| `InMemoryEventStore`            | `InMemoryEventStore` (test scope)                                     |
| EF Core `EventStoreDbContext`   | R2DBC `EventStoreRepository` over PostgreSQL                          |
| `StoredEventEnvelope`           | `StoredEventEnvelope`                                                 |
| `EventStream`                   | `EventStream`                                                         |
| `ConcurrencyException`          | `OptimisticLockException` (Spring Data) / `ConcurrencyException`      |
