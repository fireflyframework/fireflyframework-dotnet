# FireflyFramework.EventSourcing

Event-sourced aggregates with optimistic concurrency, snapshots,
projections, transactional outbox, and event upcasting. The full
event-sourcing toolkit on a Postgres / SQL Server / in-memory backend.

Mirrors `org.fireflyframework:firefly-event-sourcing-spring-boot-starter`.
The wire shape — event envelopes, schema columns, outbox semantics —
is identical across runtimes.

---

## Why event sourcing?

For a class of business problems (financial transactions, regulated
state machines, audit-heavy domains) the *history* of changes
matters as much as the current state. Event sourcing addresses this
by:

1. **Persisting every change as an immutable event** rather than
   overwriting state in place.
2. **Reconstructing state by replaying** the event history.
3. **Producing read models** asynchronously from the event stream.

This module provides the machinery: aggregate base class, event
store with optimistic concurrency, snapshot store, projection
runner, transactional outbox for reliable event publishing, and an
upcasting pipeline for schema evolution.

If your domain doesn't need the audit history or the read-model
flexibility, you don't need this module. Plain CQRS over EF Core is
fine — see `FireflyFramework.Data` and `FireflyFramework.Cqrs`. But
when you do need it, doing it right is hard, and this module is the
framework's answer.

---

## Mental model

```
                    ┌──────────────────────────────────────────┐
                    │                Aggregate                 │
                    │                                          │
                    │   .Place(...)  ──► ApplyChange(event)    │
                    │                    UncommittedChanges []  │
                    └────────────────────┬─────────────────────┘
                                         │
                                         │ AppendEventsAsync(id, type, changes, expectedVersion)
                                         ▼
                    ┌──────────────────────────────────────────┐
                    │              IEventStore                 │
                    │                                          │
                    │   firefly_events table (append-only)     │
                    │   firefly_snapshots table                │
                    │   firefly_event_outbox table             │
                    │                                          │
                    │   throws ConcurrencyException on stale   │
                    │   expectedVersion                        │
                    └────┬───────────────────────┬─────────────┘
                         │                       │
                         ▼                       ▼
            ┌────────────────────┐    ┌───────────────────────────┐
            │ ProjectionRunner   │    │ EventOutboxProcessor      │
            │ (BackgroundService)│    │ (BackgroundService)       │
            │                    │    │                           │
            │ polls events,      │    │ drains outbox table,      │
            │ feeds IProjections │    │ publishes via             │
            │ persists checkpoint│    │ IEventPublisher (EDA)     │
            └────────────────────┘    └───────────────────────────┘
```

The aggregate is the unit of consistency: changes are made by
calling business-rule methods, which produce events; the events
are appended atomically with optimistic-concurrency check on the
aggregate's version. Two background services drain the resulting
write stream into projections (read models) and into the outbox
(asynchronous publishing for downstream consumers).

---

## Quick start

### Authoring an aggregate

```csharp
using FireflyFramework.EventSourcing.Annotations;
using FireflyFramework.EventSourcing.Domain;

[DomainEvent("OrderPlaced", Version = 1)]
public sealed record OrderPlaced(
    Guid AggregateId, DateTimeOffset Timestamp,
    string CustomerId, decimal Total)
    : AbstractDomainEvent(AggregateId, Timestamp);

[Aggregate("Order")]
public sealed class Order : AggregateRoot
{
    public string?  CustomerId { get; private set; }
    public decimal  Total      { get; private set; }

    public static Order Place(Guid id, string customerId, decimal total)
    {
        var order = new Order();
        order.ApplyChange(new OrderPlaced(id, DateTimeOffset.UtcNow, customerId, total));
        return order;
    }

    // Reflectively dispatched by AggregateRoot.Apply.
    private void On(OrderPlaced e)
    {
        Id          = e.AggregateId;
        CustomerId  = e.CustomerId;
        Total       = e.Total;
    }
}
```

`ApplyChange` does two things: append the event to
`UncommittedChanges` and dispatch it to the matching `On(SpecificEvent)`
method via reflection. The state mutation is a side effect of
applying the event — replaying history reaches the same final state
deterministically.

### Saving and loading

```csharp
using FireflyFramework.EventSourcing.Store;

IEventStore store = sp.GetRequiredService<IEventStore>();

// Save.
var order = Order.Place(Guid.NewGuid(), "C-1", 199m);
await store.AppendEventsAsync(
    order.Id, order.AggregateType,
    order.UncommittedChanges,
    expectedVersion: -1,                // -1 means "new aggregate"
    ct: ct);
order.MarkChangesAsCommitted();

// Load.
var stream = await store.LoadEventStreamAsync(order.Id, "Order", ct: ct);
var rehydrated = new Order();
rehydrated.LoadFromHistory(stream.Events);
```

`AppendEventsAsync` throws `ConcurrencyException` if `expectedVersion`
does not match the persisted version of the aggregate, giving you
proper optimistic concurrency control. The pattern is:

1. Load the aggregate.
2. Mutate it.
3. Save with `expectedVersion = the version you loaded`.
4. If `ConcurrencyException`, re-load and retry.

---

## Public surface

### Domain layer

| Type | Purpose |
|---|---|
| `AggregateRoot` | Base class with `Id`, `AggregateType`, `Version`, `UncommittedChanges`, `ApplyChange`, `LoadFromHistory`, `MarkChangesAsCommitted` |
| `IDomainEvent` / `AbstractDomainEvent` | Contract: `AggregateId`, `Timestamp`, `EventType`, `EventVersion` |
| `[DomainEvent("name", Version = N)]` | Stable type discriminator + version (used during upcasting) |
| `[Aggregate("name")]` | Stable aggregate type discriminator |

Event-handler methods are conventional `private void On(SpecificEvent e)`
methods, matched reflectively by `AggregateRoot.Apply`.

### Event store

| Type | Purpose |
|---|---|
| `IEventStore` | Append + load + stream |
| `StoredEventEnvelope` | Persisted record: `GlobalSequence`, `AggregateId`, `AggregateVersion`, `AggregateType`, `EventType`, `EventVersion`, `Payload`, `Headers`, `Timestamp`, `TenantId` |
| `EventStream` | `(AggregateId, AggregateType, Events, Version)` tuple |
| `ConcurrencyException` | Thrown when `expectedVersion` does not match |
| `InMemoryEventStore` | Reference implementation for tests |
| `EfCoreEventStore` | Postgres / SQL Server-backed; append-only writes; outbox row per event |
| `EventStoreDbContext` | EF Core context for the persistent store |

### Snapshots

| Type | Purpose |
|---|---|
| `ISnapshotStore` | Save / load snapshot per aggregate |
| `EfCoreSnapshotStore` | EF Core implementation |

Take a snapshot when an aggregate's event count crosses a threshold
to avoid replaying the full history on load. Load with the
snapshot, then replay only the events with `Version >
snapshot.Version`.

### Projections

| Type | Purpose |
|---|---|
| `IProjection` | `ApplyAsync(envelope)` to update a read model |
| `IProjectionCheckpointStore` | Persist last-processed `GlobalSequence` per projection |
| `InMemoryProjectionCheckpointStore` | Default in-memory implementation |
| `ProjectionRunner` | `BackgroundService` that polls the event store, applies events to every registered `IProjection`, persists checkpoints |

### Outbox

| Type | Purpose |
|---|---|
| `EventOutboxProcessor` | `BackgroundService` that drains the outbox table and republishes via `IEventPublisher` (at-least-once semantics) |

### Upcasting

| Type | Purpose |
|---|---|
| `IEventUpcaster` | Migrate an event from one schema version to the next |
| `EventUpcastingService` | Pipeline that runs every applicable upcaster in order |

When you change an event's schema (e.g. add a new required field),
register an upcaster that knows how to read the old format and
produce the new one. The upcasting service runs on every load, so
historical events deserialise transparently into the new shape.

---

## Schema

The EF Core implementation provisions three tables:

```sql
CREATE TABLE firefly_events (
  global_sequence    BIGSERIAL PRIMARY KEY,
  aggregate_id       UUID         NOT NULL,
  aggregate_version  BIGINT       NOT NULL,
  aggregate_type     VARCHAR(255) NOT NULL,
  event_type         VARCHAR(255) NOT NULL,
  event_version      INT          NOT NULL,
  payload            TEXT         NOT NULL,
  headers_json       TEXT,
  timestamp          TIMESTAMPTZ  NOT NULL,
  tenant_id          VARCHAR(64),
  CONSTRAINT ux_aggregate UNIQUE (aggregate_type, aggregate_id, aggregate_version)
);

CREATE TABLE firefly_snapshots (
  id                 UUID         PRIMARY KEY,
  aggregate_id       UUID         NOT NULL,
  snapshot_type      VARCHAR(255) NOT NULL,
  aggregate_version  BIGINT       NOT NULL,
  payload            TEXT         NOT NULL,
  timestamp          TIMESTAMPTZ  NOT NULL
);

CREATE TABLE firefly_event_outbox (
  id              UUID PRIMARY KEY,
  global_sequence BIGINT,
  event_type      VARCHAR(255),
  destination     VARCHAR(255),
  payload         TEXT,
  published       BOOLEAN,
  created_at      TIMESTAMPTZ,
  published_at    TIMESTAMPTZ
);
```

The `ux_aggregate` unique constraint is what enforces optimistic
concurrency at the database level — appending two different events
with the same `(aggregate_type, aggregate_id, aggregate_version)`
violates the constraint, the EF Core writer maps that to
`ConcurrencyException`.

---

## Wiring (production)

```csharp
using FireflyFramework.EventSourcing.Store;
using FireflyFramework.EventSourcing.Store.EfCore;

builder.Services.AddDbContextFactory<EventStoreDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration["Firefly:Data:ConnectionString"]));

builder.Services.AddSingleton<IEventStore>(sp => new EfCoreEventStore(
    sp.GetRequiredService<IDbContextFactory<EventStoreDbContext>>(),
    knownEventTypes: new[]
    {
        typeof(OrderPlaced),
        typeof(OrderShipped),
        // ... every event type
    }));

builder.Services.AddSingleton<ISnapshotStore, EfCoreSnapshotStore>();
builder.Services.AddHostedService<EventOutboxProcessor>();
builder.Services.AddHostedService<ProjectionRunner>();
```

`Starter.Domain` does most of the wiring for you — you only need
`builder.Services.AddFireflyDomain(builder.Configuration)` plus the
list of known event types.

---

## Common patterns

### The load-mutate-save cycle

```csharp
public async Task<bool> ShipAsync(Guid orderId, CancellationToken ct)
{
    var stream = await _store.LoadEventStreamAsync(orderId, "Order", ct);
    if (stream.Events.Count == 0) return false;

    var order = new Order();
    order.LoadFromHistory(stream.Events);

    if (!order.CanShip()) throw new BusinessException("order is not ready to ship");

    order.Ship();                                         // produces OrderShipped event
    await _store.AppendEventsAsync(
        order.Id, order.AggregateType,
        order.UncommittedChanges,
        expectedVersion: stream.Version,                  // version we loaded
        ct);
    order.MarkChangesAsCommitted();
    return true;
}
```

The pattern is rigid because consistency is rigid: load → check
business rule → mutate → save with concurrency check → mark
committed. Skipping any step is a bug.

### Building a read model with `IProjection`

```csharp
public sealed class OrderListProjection(IDbContextFactory<ReadModelDbContext> factory)
    : IProjection
{
    public string Name => "OrderList";

    public async Task ApplyAsync(StoredEventEnvelope envelope, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        switch (envelope.EventType)
        {
            case "OrderPlaced":
            {
                var e = JsonSerializer.Deserialize<OrderPlaced>(envelope.Payload)!;
                db.Orders.Add(new OrderRow(e.AggregateId, e.CustomerId, e.Total, "Placed"));
                break;
            }
            case "OrderShipped":
            {
                var e = JsonSerializer.Deserialize<OrderShipped>(envelope.Payload)!;
                var row = await db.Orders.FindAsync(new object[] { e.AggregateId }, ct);
                if (row is not null) row.Status = "Shipped";
                break;
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
```

`ProjectionRunner` polls the event store and feeds every event to
each registered `IProjection`. Checkpointing is per-projection, so
adding a new read model lets it back-fill from the start of the
stream without disturbing existing projections.

### Schema evolution with an upcaster

```csharp
public sealed class OrderPlacedV1ToV2 : IEventUpcaster
{
    public string EventType => "OrderPlaced";
    public int FromVersion  => 1;
    public int ToVersion    => 2;

    public StoredEventEnvelope Upcast(StoredEventEnvelope source)
    {
        var v1 = JsonSerializer.Deserialize<OrderPlacedV1>(source.Payload)!;
        var v2 = new OrderPlacedV2(
            v1.AggregateId, v1.Timestamp,
            v1.CustomerId, v1.Total,
            Currency: "USD");                    // sensible default for old events
        return source with
        {
            EventVersion = 2,
            Payload      = JsonSerializer.Serialize(v2),
        };
    }
}
```

The upcaster runs in `EventUpcastingService` on every load, so
historical events automatically deserialise into the v2 shape — no
data migration needed. New events go straight to v2 and skip the
upcaster.

### Snapshotting frequently-loaded aggregates

```csharp
if (order.Version % 100 == 0)
{
    await _snapshotStore.SaveAsync(
        new Snapshot(
            AggregateId: order.Id,
            SnapshotType: "Order",
            Version: order.Version,
            Payload: JsonSerializer.Serialize(order),
            Timestamp: DateTimeOffset.UtcNow), ct);
}
```

On load, the loader fetches the snapshot first, hydrates the
aggregate from it, then loads only events newer than the snapshot.

---

## Pitfalls and gotchas

**Don't update aggregate state outside an event handler.** The
discipline is "every state change goes through `ApplyChange`". If
you mutate state directly, replaying the history won't reach the
same state and your read models drift.

**`expectedVersion: -1` is for new aggregates only.** Passing -1
when the aggregate already exists succeeds the first time and fails
forever after — but the failure mode is "the new event slot is
already taken", not a clean concurrency error. Always pass the
version you loaded.

**Don't read your own writes through projections immediately.**
Projections are eventually consistent. After `AppendEventsAsync`
returns, the projection runner may not have applied the event yet.
For "I just wrote it, can I read it?" patterns, hydrate the aggregate
directly from the event store.

**The outbox is at-least-once.** Downstream consumers must handle
duplicates idempotently. Pair with `IWebhookIdempotencyService` from
the webhooks module if the consumer is your own service.

**Snapshot serialisation must be backward-compatible.** A snapshot
written by version *N* of the aggregate must deserialise back into
version *N+1* of the type. Either keep the snapshot type stable or
store a schema version and migrate snapshots like you do events.

**`InMemoryEventStore` doesn't enforce the unique constraint.** It
does its own version check but the `ConcurrencyException` shape
matches. Use it for tests; never for production.

---

## Internals (for the curious)

`AggregateRoot.ApplyChange` reflects the matching `On(TEvent)` once
per type and caches the `MethodInfo` in a static
`ConcurrentDictionary<(Type aggregate, Type event), MethodInfo>`. The
reflection cost is paid once per (aggregate type, event type) pair
across the whole process; subsequent applies hit the cache.

`EfCoreEventStore.AppendEventsAsync` opens a transaction, inserts
every event row plus an outbox row, and commits. The unique
constraint on `(aggregate_type, aggregate_id, aggregate_version)` is
how we get atomic concurrency check across multiple events. We don't
read-then-check-then-write — that would race.

`ProjectionRunner` polls in batches with `LIMIT 100 ORDER BY
global_sequence`. It applies the batch in a single transaction per
projection so a failure mid-batch rolls back to the last successful
checkpoint. Idempotent projections survive replays without
duplicate read-model rows.

`EventOutboxProcessor` uses `SELECT ... FOR UPDATE SKIP LOCKED` so
multiple replicas of the same service can run the processor
without coordinating — each replica grabs distinct outbox rows. The
skip-locked semantics are why this works on Postgres and SQL Server
but not on MySQL (which doesn't have skip-locked yet).

The schema column types are deliberately `TEXT` rather than `JSONB`
because the framework targets multiple databases. If you're
Postgres-only and want JSONB indexes, alter the column at deployment
time — the `EfCoreEventStore` reads / writes string-shaped JSON
either way.

---

## Dependencies

| Reference | Used for |
|---|---|
| `FireflyFramework.Kernel` (project) | Base exceptions |
| `FireflyFramework.Eda` (project) | `IEventPublisher` for the outbox processor |
| `Microsoft.EntityFrameworkCore` (NuGet) | Persistent store |
| `Microsoft.EntityFrameworkCore.Relational` (NuGet) | Provider abstractions |
| `Microsoft.EntityFrameworkCore.InMemory` (NuGet) | Test backing |
| `System.Text.Json` (BCL) | Event payload serialisation |

The Postgres / SQL Server / MySQL providers are *not* directly
referenced — consumers pick whichever they need and pass it through
`UseNpgsql(...)` / `UseSqlServer(...)` etc. on
`AddDbContextFactory<EventStoreDbContext>`.

---

## Java mapping

| .NET | Java |
|---|---|
| `AggregateRoot` | `AggregateRoot` |
| `IDomainEvent` / `AbstractDomainEvent` | `Event` / `AbstractEvent` |
| `IEventStore` | `EventStore` |
| `ConcurrencyException` | `ConcurrencyException` |
| `EfCoreEventStore` | `R2dbcEventStore` (the Java line uses R2DBC; .NET uses EF Core) |
| `EventOutboxProcessor` | `EventOutboxProcessor` |
| `ProjectionRunner` | `ProjectionService` + `ProjectionProcessor` |
| `IEventUpcaster` | `EventUpcaster` |
| `[DomainEvent]` | `@DomainEvent` |
| `[Aggregate]` | `@Aggregate` |

The schema is identical between runtimes — a Postgres database
populated by a Java service is read correctly by a .NET service and
vice versa.

---

## See also

* [`FireflyFramework.Eda`](../FireflyFramework.Eda/README.md) — the publisher used by `EventOutboxProcessor`.
* [`FireflyFramework.Cqrs`](../FireflyFramework.Cqrs/README.md) — commands typically write to event-sourced aggregates.
* [`FireflyFramework.Starter.Domain`](../FireflyFramework.Starter.Domain/README.md) — one-call wiring for a domain-tier service that uses event sourcing.
* [`docs/CONFIGURATION.md`](../../docs/CONFIGURATION.md) — `Firefly:EventSourcing:*` reference.
