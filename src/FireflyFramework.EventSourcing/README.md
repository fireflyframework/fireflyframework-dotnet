# FireflyFramework.EventSourcing

Event-sourced aggregates with optimistic concurrency, snapshots,
projections, transactional outbox, and event upcasting. Mirrors
`org.fireflyframework:firefly-event-sourcing-spring-boot-starter`.

## Authoring an aggregate

```csharp
using FireflyFramework.EventSourcing.Annotations;
using FireflyFramework.EventSourcing.Domain;

[DomainEvent("OrderPlaced", Version = 1)]
public sealed record OrderPlaced(Guid AggregateId, DateTimeOffset Timestamp, string CustomerId, decimal Total)
    : AbstractDomainEvent(AggregateId, Timestamp);

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

    private void On(OrderPlaced e)
    {
        Id          = e.AggregateId;
        CustomerId  = e.CustomerId;
        Total       = e.Total;
    }
}
```

## Saving and loading

```csharp
using FireflyFramework.EventSourcing.Store;

IEventStore store = sp.GetRequiredService<IEventStore>();

// Save
var order = Order.Place(Guid.NewGuid(), "C-1", 199m);
await store.AppendEventsAsync(order.Id, order.AggregateType, order.UncommittedChanges, expectedVersion: -1, ct: ct);
order.MarkChangesAsCommitted();

// Load
var stream  = await store.LoadEventStreamAsync(order.Id, "Order", ct: ct);
var rehydr  = new Order();
rehydr.LoadFromHistory(stream.Events);
```

`AppendEventsAsync` throws `ConcurrencyException` if `expectedVersion`
does not match the persisted version of the aggregate, giving you proper
optimistic concurrency control.

## Public surface

### Domain layer

| Type                                    | Purpose                                                                 |
|-----------------------------------------|-------------------------------------------------------------------------|
| `AggregateRoot`                         | Base class with `Id`, `AggregateType`, `Version`, `UncommittedChanges`, `ApplyChange`, `LoadFromHistory`, `MarkChangesAsCommitted` |
| `IDomainEvent` / `AbstractDomainEvent`  | Contract: `AggregateId`, `Timestamp`, `EventType`, `EventVersion`       |
| `[DomainEvent("name", Version = N)]`    | Stable type discriminator + version (used during upcasting)             |
| `[Aggregate("name")]`                   | Stable aggregate type discriminator                                     |

Event-handler methods are conventional `private void On(SpecificEvent e)`
methods, matched reflectively by `AggregateRoot.Apply`.

### Event store

| Type                          | Purpose                                                              |
|-------------------------------|----------------------------------------------------------------------|
| `IEventStore`                 | Append + load + stream                                               |
| `StoredEventEnvelope`         | Persisted record: `GlobalSequence`, `AggregateId/Version/Type`, `EventType/Version`, `Payload`, `Headers`, `Timestamp`, `TenantId` |
| `EventStream`                 | `(AggregateId, AggregateType, Events, Version)` tuple                |
| `ConcurrencyException`        | Thrown when `expectedVersion` does not match                         |
| `InMemoryEventStore`          | Reference implementation suitable for tests                          |
| `EfCoreEventStore`            | Postgres / SqlServer-backed; append-only writes; outbox row per event |
| `EventStoreDbContext`         | EF Core context for the persistent store                             |

### Snapshots

| Type                  | Purpose                                                |
|-----------------------|--------------------------------------------------------|
| `ISnapshotStore`      | Save / load snapshot per aggregate                     |
| `EfCoreSnapshotStore` | EF Core implementation                                 |

Take a snapshot when an aggregate's event count crosses a threshold to
avoid replaying the full history on load.

### Projections

| Type                                | Purpose                                                  |
|-------------------------------------|----------------------------------------------------------|
| `IProjection`                       | `ApplyAsync(envelope)` to update a read model            |
| `IProjectionCheckpointStore`        | Persist last-processed `GlobalSequence` per projection   |
| `InMemoryProjectionCheckpointStore` | Default in-memory implementation                         |
| `ProjectionRunner`                  | `BackgroundService` that polls the event store, applies events, persists checkpoints |

### Outbox

| Type                  | Purpose                                                                 |
|-----------------------|-------------------------------------------------------------------------|
| `EventOutboxProcessor` | `BackgroundService` that drains the outbox table and republishes via `IEventPublisher` (at-least-once) |

### Upcasting

| Type                       | Purpose                                                              |
|----------------------------|----------------------------------------------------------------------|
| `IEventUpcaster`           | Migrate an event from one schema version to the next                 |
| `EventUpcastingService`    | Pipeline that runs every applicable upcaster in order                |

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

## Wiring (production)

```csharp
using FireflyFramework.EventSourcing.Store;
using FireflyFramework.EventSourcing.Store.EfCore;

builder.Services.AddDbContextFactory<EventStoreDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration["Firefly:Data:ConnectionString"]));

builder.Services.AddSingleton<IEventStore>(sp => new EfCoreEventStore(
    sp.GetRequiredService<IDbContextFactory<EventStoreDbContext>>(),
    knownEventTypes: new[] { typeof(OrderPlaced) /*, ... */ }));
```

## Dependencies

| Reference                              | Used for                       |
|----------------------------------------|--------------------------------|
| `FireflyFramework.Kernel`              | Base exceptions                |
| `FireflyFramework.Eda`                 | Outbox publisher               |
| `Microsoft.EntityFrameworkCore`        | Persistent store               |

## Java mapping

| .NET                                  | Java                                        |
|---------------------------------------|---------------------------------------------|
| `AggregateRoot`                       | `AggregateRoot`                             |
| `IDomainEvent` / `AbstractDomainEvent` | `Event` / `AbstractEvent`                  |
| `IEventStore`                         | `EventStore`                                |
| `ConcurrencyException`                | `ConcurrencyException`                      |
| `EfCoreEventStore`                    | `R2dbcEventStore`                           |
| `EventOutboxProcessor`                | `EventOutboxProcessor`                      |
| `ProjectionRunner`                    | `ProjectionService` + `ProjectionProcessor` |
| `IEventUpcaster`                      | `EventUpcaster`                             |
