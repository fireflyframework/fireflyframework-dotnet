# Sample: Orders Service

A minimal ASP.NET Core 9 microservice that demonstrates the Firefly Framework end-to-end:

- One-line wiring via `AddFireflyCore` (Web + Observability + Cache + EDA + CQRS)
- A command handler (`PlaceOrderHandler`) with built-in validation
- A cacheable query (`GetOrderQuery`) — second call hits the cache
- Idempotency middleware on `POST /api/orders` (send `X-Idempotency-Key` to dedupe)
- RFC 7807 problem-details on validation failures
- OpenAPI document at `/openapi/v1.json`

## Run

```bash
source ../../.envrc
dotnet run --project samples/FireflyFramework.Samples.OrdersService

# Place an order
curl -X POST http://localhost:5000/api/orders \
  -H 'Content-Type: application/json' \
  -H 'X-Idempotency-Key: demo-123' \
  -d '{"sku":"SKU-1","quantity":2,"unitPrice":12.50}'

# Read it back
curl http://localhost:5000/api/orders/<id-from-previous-response>
```

## What it shows

| File | Demonstrates |
|---|---|
| `Program.cs` | `AddFireflyCore`, minimal-API endpoints, command + query dispatch |
| `PlaceOrderCommand` | `ICommand<TResult>` with overridden `ValidateAsync` |
| `GetOrderQuery` | `IQuery<TResult>` with `IsCacheable=true` |
| `appsettings.json` | Wiring of every Firefly section |
