# FireflyFramework.Samples.OrdersService.Core

The **business-logic** project. Commands, queries, handlers, mappers,
and any pure-domain services live here.

## Contents

```
Mappers/
  OrderMapper.cs                 # OrderEntity ↔ OrderDto
Services/Orders/V1/
  PlaceOrderCommand.cs           # ICommand<Guid>, with overridden ValidateAsync
  PlaceOrderHandler.cs           # ICommandHandler<PlaceOrderCommand, Guid>
  GetOrderQuery.cs               # IQuery<OrderDto?> with IsCacheable = true
  GetOrderHandler.cs             # IQueryHandler<GetOrderQuery, OrderDto?>
```

## Why commands and queries instead of services

The framework's `ICommandBus` and `IQueryBus` give you for free:

- Validation (via `ValidateAsync`)
- Authorization (via `IAuthorizer<T>` if registered)
- Per-query result caching (via `IsCacheable`/`CacheKey`/`CacheTtl`)
- OpenTelemetry tracing of every dispatch
- Cache invalidation on domain events (`EventDrivenCacheInvalidator`)

A traditional `IOrderService.PlaceOrderAsync` cannot opt into any of
these without re-implementing the cross-cutting machinery.

## Java mapping

| .NET                                            | Java                                              |
|-------------------------------------------------|---------------------------------------------------|
| `Core.Services.Orders.V1.PlaceOrderCommand`     | `core.services.orders.v1.PlaceOrderCommand`       |
| `Core.Services.Orders.V1.PlaceOrderHandler`     | `core.services.orders.v1.PlaceOrderHandler`       |
| `Core.Mappers.OrderMapper`                      | `core.mappers.OrderMapper` (MapStruct)            |
