# FireflyFramework.Samples.OrdersService.Web

## Overview

The runnable **ASP.NET Core 10 host** for the Orders sample. This is
the only project that has a `Program.cs` and produces an executable;
the other four are libraries the Web project composes.

`Program.cs` is intentionally short — `AddFireflyCore` activates the
entire infrastructure tier in one call, the controller is a thin
binding layer, and everything else lives in `Core` / `Models` /
`Interfaces`. That's the shape every Firefly service should reach
for: small host, rich library tiers.

## Mental model

```
                     HTTP request
                          │
                          ▼
    ┌───────────────────────────────────────┐
    │ ASP.NET 10 pipeline                   │
    │  - PII masking middleware             │
    │  - Correlation-id middleware          │
    │  - Idempotency middleware             │
    │  - RFC 7807 problem-details handler   │
    └────────────┬──────────────────────────┘
                 │
                 ▼
    ┌───────────────────────────────────────┐
    │ OrdersController                      │
    │   POST → bus.DispatchAsync(cmd)       │
    │   GET  → bus.DispatchAsync(query)     │
    └────────────┬──────────────────────────┘
                 │
                 ▼
    ┌───────────────────────────────────────┐
    │ Core handlers                          │
    │   PlaceOrderHandler (writes)           │
    │   GetOrderHandler (reads, cached)      │
    └────────────┬──────────────────────────┘
                 │
                 ▼
    ┌───────────────────────────────────────┐
    │ IOrderRepository (Models)              │
    │  in-memory by default                  │
    └────────────────────────────────────────┘
```

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
Health checks at `/health/live` and `/health/ready`.

## Demo flow

The two-curl POST + replay demonstrates **idempotency**: the second
call returns the original response with the original event id —
without re-running the command handler. The framework's idempotency
middleware caches the response under the supplied
`X-Idempotency-Key` for 24 hours.

The two-curl GET demonstrates the **query cache**: the first call
runs the query handler; the second call returns the cached result
because `GetOrderQuery.IsCacheable = true`. Run `curl /metrics` and
watch `firefly.cqrs.query.cache.hit` increment.

## Going further

| To enable                             | Add                                                                   |
|---------------------------------------|-----------------------------------------------------------------------|
| Persistent storage                    | `AddFireflyData` from `Starter.Data` + an EF Core `DbContext`         |
| Event sourcing aggregates             | `AddFireflyDomain` from `Starter.Domain`                              |
| OAuth / OIDC                          | Register `KeycloakIdpAdapter` / `AzureAdIdpAdapter` / etc.            |
| Real broker                           | Set `Firefly:Eda:Provider` to `Kafka` or `RabbitMq`                   |
| Saga orchestration                    | Reference `FireflyFramework.Orchestration`, register engines           |
| Plugin loading                        | `Starter.Application` + `AssemblyPluginLoader`                        |

## Common patterns

### Layering an auth policy

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority = builder.Configuration["Auth:Authority"];
        o.Audience  = builder.Configuration["Auth:Audience"];
    });

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("orders:place", p => p.RequireScope("orders:place"));
});

app.MapPost("/api/v1/orders", PlaceOrder)
    .RequireAuthorization("orders:place");
```

### Swapping the in-memory repo for EF Core

```csharp
builder.Services.AddDbContext<OrdersDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Orders")));
builder.Services.AddScoped<IOrderRepository, EfCoreOrderRepository>();
```

(Drop the in-memory registration first — the framework picks the
last-registered `IOrderRepository`.)

### Mapping idempotency to a real cache

```json
{
  "Firefly": {
    "Cache": {
      "Provider":  "Redis",
      "Redis":     { "ConnectionString": "redis-master:6379" }
    }
  }
}
```

The idempotency middleware uses `ICacheAdapter`; switching to Redis
is a config change.

## Pitfalls and gotchas

- **The Web project has the only `Program.cs`.** Don't add a second
  one to `Core` or `Models` — those should be libraries.
- **`AddFireflyCore` reads the `Firefly:*` configuration sections
  on startup.** A typo in `appsettings.json` won't crash; it'll
  silently fall back to defaults. Run with verbose logging during
  local dev to spot the warnings.
- **The Dockerfile uses multi-stage build.** The runtime image
  ships only the published assemblies, not the SDK. Keep build-time
  `RUN` commands minimal — every layer adds size.
- **Don't reference `.Models` from `.Web` directly.** Go through
  `.Core`. The compiler enforces this transitively but it's tempting
  to bypass for "just one query."

## Java mapping

| .NET                                  | Java                                  |
|---------------------------------------|---------------------------------------|
| `Program.cs`                          | `OrdersServiceApplication.java`       |
| `Web.Controllers.OrdersController`    | `web.controllers.OrdersController`    |
