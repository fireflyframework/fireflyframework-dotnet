# FireflyFramework.Samples.OrdersService.Web

The runnable ASP.NET Core 9 host. Wires the framework, registers
infrastructure adapters, and exposes HTTP endpoints.

## Contents

```
Program.cs           # AddFireflyCore + endpoint mapping
appsettings.json     # every Firefly:* configuration section
Dockerfile           # multi-stage build for the runtime image
```

## What `AddFireflyCore` wires

A single call activates the entire infrastructure tier:

| Module          | Brings                                                              |
|-----------------|---------------------------------------------------------------------|
| `Web`           | RFC 7807 problem-details, correlation IDs, idempotency, PII masking |
| `Cache`         | `ICacheAdapter` + `FireflyCacheManager`                             |
| `Observability` | OpenTelemetry tracing / metrics / logs                              |
| `Eda`           | In-memory event bus (override via `Firefly:Eda:Provider`)           |
| `Cqrs`          | Command + query buses with handler discovery                        |

## Run

```bash
source ../../.envrc
dotnet run --project samples/FireflyFramework.Samples.OrdersService.Web

# Place an order
curl -X POST http://localhost:5000/api/v1/orders \
  -H 'Content-Type: application/json' \
  -H 'X-Idempotency-Key: demo-123' \
  -d '{"sku":"SKU-1","quantity":2,"unitPrice":12.50}'

# Replay (returns the cached response from the idempotency middleware)
curl -X POST http://localhost:5000/api/v1/orders \
  -H 'Content-Type: application/json' \
  -H 'X-Idempotency-Key: demo-123' \
  -d '{"sku":"SKU-1","quantity":2,"unitPrice":12.50}'

# Read it back (second read hits the query cache)
curl http://localhost:5000/api/v1/orders/<id-from-previous-response>
```

OpenAPI is at `/openapi/v1.json`. Prometheus metrics at `/metrics`.

## Going further

| To enable                             | Add                                                                   |
|---------------------------------------|-----------------------------------------------------------------------|
| Persistent storage                    | `AddFireflyData` from `Starter.Data` + an EF Core `DbContext`         |
| Event sourcing aggregates             | `AddFireflyDomain` from `Starter.Domain`                              |
| OAuth / OIDC                          | Register `KeycloakIdpAdapter` / `AzureAdIdpAdapter` / etc.            |
| Real broker                           | Set `Firefly:Eda:Provider` to `Kafka` or `RabbitMq`                   |
