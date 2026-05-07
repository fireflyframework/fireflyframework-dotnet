# FireflyFramework.EventSourcing

Event-sourced aggregates with optimistic concurrency, snapshots, transactional outbox and projections. Mirrors `fireflyframework-eventsourcing`.

## Quick start

```csharp
[DomainEvent("OrderPlaced")]
public sealed record OrderPlaced(Guid AggregateId, DateTimeOffset Timestamp, string CustomerId, decimal Total)
    : AbstractDomainEvent(AggregateId, Timestamp);

public sealed class Order : AggregateRoot
{
    public string? CustomerId { get; private set; }
    public decimal Total { get; private set; }

    public static Order Place(Guid id, string customer, decimal total)
    {
        var o = new Order();
        o.ApplyChange(new OrderPlaced(id, DateTimeOffset.UtcNow, customer, total));
        return o;
    }

    private void On(OrderPlaced e)
    {
        Id = e.AggregateId;
        CustomerId = e.CustomerId;
        Total = e.Total;
    }
}

// Use it
var order = Order.Place(Guid.NewGuid(), "C-1", 199m);
await store.AppendEventsAsync(order.Id, order.AggregateType, order.UncommittedChanges, expectedVersion: -1);
order.MarkChangesAsCommitted();
```

## What's inside

| Type | Purpose |
|---|---|
| `AggregateRoot` | Base class for event-sourced aggregates. Subclasses emit events with `ApplyChange` and reload state via `LoadFromHistory`. Event handlers are conventional `private void On(SpecificEvent e)` methods (matched reflectively). |
| `IDomainEvent` + `AbstractDomainEvent` | Domain event contract; `EventType` defaults to the value of `[DomainEvent("…")]`. |
| `[DomainEvent("…")]` | Tags an event class with a stable type discriminator + version. |
| `IEventStore` | Append-only event store contract: `AppendEventsAsync`, `LoadEventStreamAsync`, `GetAggregateVersionAsync`, `StreamAllEventsAsync`, `StreamAllEventsFromAsync`. |
| `ConcurrencyException` | Raised when `expectedVersion` does not match the persisted version. |
| `InMemoryEventStore` | In-process implementation suitable for tests. |
| `EfCoreEventStore` | Production-ready EF Core implementation — append-only writes, optimistic concurrency via unique `(aggregateType, aggregateId, version)` index, transactional outbox row per event. Wires through `EventStoreDbContext`. |
| `ISnapshotStore` + `EfCoreSnapshotStore` | Snapshot persistence with configurable retention. |
| `TenantContext` | Ambient tenant id propagated via `AsyncLocal<T>` (replaces Reactor Context). |

## Schema (EF Core)

```sql
CREATE TABLE firefly_events (
  global_sequence    BIGSERIAL PRIMARY KEY,
  aggregate_id       UUID NOT NULL,
  aggregate_version  BIGINT NOT NULL,
  aggregate_type     VARCHAR(255) NOT NULL,
  event_type         VARCHAR(255) NOT NULL,
  event_version      INT NOT NULL,
  payload            TEXT NOT NULL,
  headers_json       TEXT,
  timestamp          TIMESTAMPTZ NOT NULL,
  tenant_id          VARCHAR(64),
  CONSTRAINT ux_aggregate UNIQUE (aggregate_type, aggregate_id, aggregate_version)
);

CREATE TABLE firefly_snapshots (id UUID PK, aggregate_id UUID, snapshot_type VARCHAR(255), aggregate_version BIGINT, payload TEXT, timestamp TIMESTAMPTZ);
CREATE TABLE firefly_event_outbox (id UUID PK, global_sequence BIGINT, event_type VARCHAR(255), destination VARCHAR(255), payload TEXT, published BOOLEAN, created_at TIMESTAMPTZ, published_at TIMESTAMPTZ);
```

## Wiring EF Core

```csharp
builder.Services.AddDbContextFactory<EventStoreDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("FireflyEventStore")));
builder.Services.AddSingleton<IEventStore>(sp => new EfCoreEventStore(
    sp.GetRequiredService<IDbContextFactory<EventStoreDbContext>>(),
    knownEventTypes: new[] { typeof(OrderPlaced), /* ... */ }));
builder.Services.AddSingleton<ISnapshotStore, EfCoreSnapshotStore>();
```
