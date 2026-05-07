# Sample: Orders Service

A runnable ASP.NET Core 9 microservice that demonstrates the Firefly
Framework end-to-end.

## What it covers

- One-line wiring via `AddFireflyCore` — Web, Cache, Observability,
  EDA, CQRS in a single call.
- A command (`PlaceOrderCommand`) with validation in `ValidateAsync`.
- A query (`GetOrderQuery`) opted into caching with
  `IsCacheable = true`, `CacheKey = $"order:{OrderId}"`,
  `CacheTtl = 5 minutes`.
- Idempotency middleware: `POST /api/orders` honours
  `X-Idempotency-Key` and returns the cached response on retries.
- RFC 7807 `application/problem+json` responses on validation failures.
- OpenAPI document at `/openapi/v1.json`.

## Run

```bash
source ../../.envrc
dotnet run --project samples/FireflyFramework.Samples.OrdersService

# Place an order
curl -X POST http://localhost:5000/api/orders \
  -H 'Content-Type: application/json' \
  -H 'X-Idempotency-Key: demo-123' \
  -d '{"sku":"SKU-1","quantity":2,"unitPrice":12.50}'

# Replay (returns the same response from the idempotency cache)
curl -X POST http://localhost:5000/api/orders \
  -H 'Content-Type: application/json' \
  -H 'X-Idempotency-Key: demo-123' \
  -d '{"sku":"SKU-1","quantity":2,"unitPrice":12.50}'

# Read it back (second read hits the query cache)
curl http://localhost:5000/api/orders/<id-from-previous-response>
```

## File map

| File                         | Demonstrates                                                      |
|------------------------------|-------------------------------------------------------------------|
| `Program.cs`                 | `AddFireflyCore`, minimal-API endpoints, command + query dispatch |
| `PlaceOrderCommand`          | `ICommand<Guid>` with overridden `ValidateAsync`                  |
| `PlaceOrderHandler`          | `ICommandHandler<PlaceOrderCommand, Guid>`                        |
| `GetOrderQuery`              | `IQuery<OrderDto?>` with `IsCacheable = true`                     |
| `GetOrderHandler`            | `IQueryHandler<GetOrderQuery, OrderDto?>`                         |
| `InMemoryOrderRepository`    | Trivial concurrent-dictionary store (replace with EF Core in a real service) |
| `appsettings.json`           | Wiring of every `Firefly:*` configuration section used            |

## Going further

To upgrade this sample to a production-shaped service:

1. Replace `InMemoryOrderRepository` with an EF Core `DbContext` and
   call `AddFireflyData` from `FireflyFramework.Starter.Data`.
2. Switch CQRS to event-sourcing by inheriting from `AggregateRoot` and
   calling `AddFireflyDomain` from `FireflyFramework.Starter.Domain`.
3. Pick an IDP adapter — register `KeycloakIdpAdapter`, `AzureAdIdpAdapter`,
   `CognitoIdpAdapter`, or `InternalDbIdpAdapter` as the singleton
   `IIdpAdapter`.
4. Set `Firefly:Eda:Provider` to `Kafka` or `RabbitMq` in
   `appsettings.json` to switch from the in-memory bus to a real broker.
