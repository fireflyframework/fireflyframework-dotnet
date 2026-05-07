# FireflyFramework.Samples.OrdersService.Core

## Overview

The **business-logic project** for the Orders sample. Commands,
queries, handlers, mappers, and any pure-domain services live here.
This is where the rules of the service are encoded — every other
project either delivers input to `Core` (`Web`) or stores the data
`Core` mutates (`Models`).

The implementation deliberately uses CQRS rather than a flat
`IOrderService` so the framework's bus machinery can layer
validation, authorisation, caching, and tracing on every dispatch
without changing handler code.

## Why commands and queries instead of services

The framework's `ICommandBus` and `IQueryBus` give you for free:

- **Validation** via `ValidateAsync` on the command/query.
- **Authorization** via `IAuthorizer<T>` if registered.
- **Per-query result caching** via `IsCacheable` / `CacheKey` /
  `CacheTtl`.
- **OpenTelemetry tracing** of every dispatch — span name is the
  command/query type name.
- **Cache invalidation on domain events** via
  `EventDrivenCacheInvalidator`.

A traditional `IOrderService.PlaceOrderAsync` cannot opt into any of
these without re-implementing the cross-cutting machinery in every
method.

## Mental model

```
                    Web layer
                       │
                       │  builder.Services.AddSingleton<ICommandHandler<…>, …>()
                       │  IServiceCollection discovers handlers
                       ▼
   ┌─────────────────────────────────────────┐
   │  ICommandBus.DispatchAsync(command)     │
   │   1. Validate (command.ValidateAsync)   │
   │   2. Authorize (IAuthorizer<T>)         │
   │   3. Span starts                        │
   │   4. handler.HandleAsync(command, ct)   │
   │   5. Span ends                          │
   └────────────┬────────────────────────────┘
                │
                ▼
   ┌─────────────────────────────────────────┐
   │  PlaceOrderHandler                       │
   │   - calls IOrderRepository (Models)      │
   │   - emits a DomainEvent                  │
   │   - returns the new order id             │
   └─────────────────────────────────────────┘
```

The same pattern applies to `IQueryBus.DispatchAsync(query)` —
plus result caching when `IsCacheable = true`.

## Contents

```
Mappers/
  OrderMapper.cs                 # OrderEntity ↔ OrderDto (pure)
Services/Orders/V1/
  PlaceOrderCommand.cs           # ICommand<Guid>, with overridden ValidateAsync
  PlaceOrderHandler.cs           # ICommandHandler<PlaceOrderCommand, Guid>
  GetOrderQuery.cs               # IQuery<OrderDto?> with IsCacheable = true
  GetOrderHandler.cs             # IQueryHandler<GetOrderQuery, OrderDto?>
```

```csharp
public sealed record PlaceOrderCommand(
    string  Sku,
    int     Quantity,
    decimal UnitPrice) : Command<Guid>
{
    public override Task<ValidationResult> ValidateAsync(CancellationToken ct = default) =>
        Task.FromResult(string.IsNullOrEmpty(Sku) || Quantity <= 0 || UnitPrice <= 0m
            ? ValidationResult.Invalid("Bad order")
            : ValidationResult.Valid());
}

public sealed class PlaceOrderHandler(IOrderRepository repo, IEventBus events)
    : ICommandHandler<PlaceOrderCommand, Guid>
{
    public async Task<Guid> HandleAsync(PlaceOrderCommand cmd, CancellationToken ct)
    {
        var entity = new OrderEntity
        {
            Id        = Guid.NewGuid(),
            Sku       = cmd.Sku,
            Quantity  = cmd.Quantity,
            UnitPrice = cmd.UnitPrice,
            Status    = OrderStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await repo.AddAsync(entity, ct);

        // Event publication enables EventDrivenCacheInvalidator to evict
        // stale GetOrderQuery results for this aggregate.
        await events.PublishAsync(new OrderPlacedEvent(entity.Id), ct);
        return entity.Id;
    }
}
```

The `Query` side is just as compact:

```csharp
public sealed record GetOrderQuery(Guid Id) : Query<OrderDto?>
{
    public override bool      IsCacheable => true;
    public override string    CacheKey    => $"order:{Id}";
    public override TimeSpan? CacheTtl    => TimeSpan.FromMinutes(5);
}

public sealed class GetOrderHandler(IOrderRepository repo, IOrderMapper mapper)
    : IQueryHandler<GetOrderQuery, OrderDto?>
{
    public async Task<OrderDto?> HandleAsync(GetOrderQuery q, CancellationToken ct) =>
        mapper.ToDto(await repo.FindByIdAsync(q.Id, ct));
}
```

## Common patterns

### Adding a new command

1. Define the command record (in `Services/Orders/V1/...`).
2. Override `ValidateAsync` for input shape checks.
3. Implement `ICommandHandler<TCommand, TResult>`.
4. Let DI auto-discover the handler — no registration needed if
   you've called `AddFireflyCqrs(typeof(Program).Assembly)`.

### Cross-aggregate workflows

Reach for the orchestration tier (`FireflyFramework.Orchestration`)
rather than chaining handlers — saga / TCC / workflow gives you
durable state, compensation, and a topology view.

### Domain events

Every command handler that mutates state should emit a domain event
via `IEventBus`. The event-driven cache invalidator subscribes
automatically so derived projections evict cleanly. Don't expose
event types from `Interfaces` unless callers should subscribe; keep
internal events internal.

## Pitfalls and gotchas

- **Don't put validation attributes on the DTO.** Validation is a
  domain rule, not a wire concern. Override `ValidateAsync` on the
  command instead.
- **Handlers are scoped by default.** If you store mutable state on
  a handler field, it lives only for the dispatch — fine for
  per-request DbContext, dangerous for cross-request caches.
- **Don't dispatch from inside a handler.** It's tempting to call
  `bus.DispatchAsync(...)` from `HandleAsync`, but this hides
  control flow and complicates tracing. Compose at the orchestration
  tier instead.
- **`IsCacheable = true` requires a stable `CacheKey`.** A key that
  changes between calls for the same logical query thrashes the
  cache. Include only fields that fully identify the result.

## Java mapping

| .NET                                            | Java                                              |
|-------------------------------------------------|---------------------------------------------------|
| `Core.Services.Orders.V1.PlaceOrderCommand`     | `core.services.orders.v1.PlaceOrderCommand`       |
| `Core.Services.Orders.V1.PlaceOrderHandler`     | `core.services.orders.v1.PlaceOrderHandler`       |
| `Core.Services.Orders.V1.GetOrderQuery`         | `core.services.orders.v1.GetOrderQuery`           |
| `Core.Services.Orders.V1.GetOrderHandler`       | `core.services.orders.v1.GetOrderHandler`         |
| `Core.Mappers.OrderMapper`                      | `core.mappers.OrderMapper` (MapStruct)            |
