# FireflyFramework.Samples.OrdersService.Sdk

## Overview

A **typed `HttpClient`** so other services can call the Orders
service in a strongly-typed, idiomatic way without re-declaring DTOs
or marshalling JSON by hand. The Sdk references only `Interfaces`,
so a consumer pulls in DTOs and nothing else — no `Models`, no EF
Core, no business logic.

This is the same boundary the Java `*-sdk` Maven module enforces. A
Java consumer of the Java OrdersService imports
`OrdersService-interfaces.jar`; a .NET consumer imports
`OrdersService.Sdk.dll` which exposes the same DTOs as records.

## Why a separate Sdk project?

Without a typed Sdk, every consumer service writes the same
boilerplate:

```csharp
var resp = await http.PostAsJsonAsync("/api/v1/orders", body);
resp.EnsureSuccessStatusCode();
var dto = await resp.Content.ReadFromJsonAsync<OrderDto>();
```

This pattern is fine *once*. With ten consumers, it's ten copies of
URL paths, ten copies of error-handling, and ten places to update
when the API changes. The Sdk centralises them:

- **One typed method per endpoint.** Consumers call
  `orders.PlaceOrderAsync(...)`, not `http.PostAsJsonAsync(...)`.
- **One place to change.** A new endpoint adds one method here; all
  consumers get it on package update.
- **One assembly to take.** No `Models`, no EF Core, no transitive
  bloat.

## Mental model

```
   consumer service
        │
        │ injects IOrdersServiceClient
        ▼
   ┌────────────────────────────┐
   │ OrdersServiceClient (Sdk)  │
   │  - typed methods           │
   │  - knows endpoint paths    │
   │  - serialises DTOs         │
   └──────────┬─────────────────┘
              │ wraps
              ▼
   ┌────────────────────────────┐
   │ HttpClient                 │
   │  (configured by the host)  │
   └──────────┬─────────────────┘
              │
              │ HTTP
              ▼
        Orders.Web service
```

Consumers compose `IHttpClientBuilder` extensions on top of
`AddOrdersServiceClient(...)` to wire resilience, auth, and
discovery — same as any typed HttpClient.

## Contents

```
IOrdersServiceClient.cs           # the contract
OrdersServiceClient.cs            # implementation
OrdersServiceClientExtensions.cs  # AddOrdersServiceClient extension
```

```csharp
public interface IOrdersServiceClient
{
    Task<OrderDto?> PlaceOrderAsync(
        PlaceOrderRequest request,
        string? idempotencyKey = null,
        CancellationToken ct = default);

    Task<OrderDto?> GetOrderAsync(Guid id, CancellationToken ct = default);
}
```

## Wire-up

```csharp
services.AddOrdersServiceClient(new Uri("https://orders.example.com/"));
```

## Use

```csharp
public sealed class CheckoutHandler(IOrdersServiceClient orders)
{
    public Task<Guid> PlaceAsync(string sku, int qty, decimal unit) =>
        orders.PlaceOrderAsync(
            new PlaceOrderRequest(sku, qty, unit),
            idempotencyKey: Guid.NewGuid().ToString())
            .ContinueWith(t => t.Result!.Id);
}
```

## Common patterns

### Adding resilience

```csharp
services.AddOrdersServiceClient(new Uri("https://orders.example.com/"))
    .AddStandardResilienceHandler();
```

The standard handler composes the Microsoft-recommended pipeline:
bulkhead → total-timeout → retry → circuit-breaker → per-attempt-timeout.

### Adding bearer auth

```csharp
services.AddOrdersServiceClient(new Uri("https://orders.example.com/"))
    .AddHttpMessageHandler(sp =>
    {
        var tokens = sp.GetRequiredService<IOAuth2TokenCache>();
        return new BearerTokenHandler(tokens, audience: "orders");
    });
```

### Discovery via Consul / Eureka / Kubernetes

```csharp
services.AddOrdersServiceClient(baseAddress: null)
    .ConfigureHttpClient(http => { /* base address resolved per request */ })
    .AddHttpMessageHandler(_ => new DiscoveryHandler(consulClient, "orders"));
```

The `ServiceClient.Rest()` builder from `FireflyFramework.Client`
handles this end-to-end if you don't want to roll your own handler.

### Stub for tests

For unit / integration tests, register a stub:

```csharp
services.AddSingleton<IOrdersServiceClient, FakeOrdersClient>();
```

Where `FakeOrdersClient` implements the interface in-memory. The
typed contract is your seam.

## Pitfalls and gotchas

- **`PlaceOrderAsync` returns `OrderDto?`.** A 404 surfaces as
  `null`; a 5xx throws `HttpRequestException` via
  `EnsureSuccessStatusCode`. Don't rely on `null` to mean
  "transport error."
- **`idempotencyKey` is optional but recommended.** A retried POST
  without an idempotency key creates a duplicate order — the server
  has no way to detect it.
- **The Sdk does not retry by default.** Wire
  `AddStandardResilienceHandler()` if you want auto-retry.
- **HTTPS verification matters.** Production builds must verify
  certificates; dev builds may need `HttpClientHandler` overrides
  for self-signed certs. Don't ship those overrides to prod.
- **The Sdk references `.Interfaces` only.** Don't add a project
  reference to `.Models` or `.Core` — that defeats the boundary.

## Java mapping

| .NET                                     | Java                                |
|------------------------------------------|-------------------------------------|
| `IOrdersServiceClient`                   | `OrdersServiceClient` (interface)   |
| `OrdersServiceClient`                    | `OrdersServiceClient`               |
| `AddOrdersServiceClient`                 | Spring Cloud OpenFeign auto-config  |
