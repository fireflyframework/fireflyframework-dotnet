# FireflyFramework.Client

## Overview

`FireflyFramework.Client` is the **service-client toolkit** for
calling other services. It bundles a fluent builder per protocol
(REST, SOAP, WebSocket, gRPC) plus the cross-cutting concerns every
production client needs: Polly v8 resilience pipelines, service
discovery (Static / Eureka / Consul / Kubernetes), client-side load
balancing, OAuth2 token caching, request deduplication, chaos
engineering, health rollup, and a GraphQL helper.

It mirrors `org.fireflyframework:firefly-service-client` from the
Java line. The fluent builder shape, resilience defaults, and
discovery integrations match Spring Cloud LoadBalancer + Resilience4j +
OpenFeign in scope, with .NET-idiomatic typing.

## Why a separate module?

Service-to-service calls cluster a half-dozen concerns that, taken
individually, are each three-line problems but together demand
careful composition: how do you discover the target, pick which
instance to hit, retry on transient failures, cache an OAuth2 token,
deduplicate inflight requests, observe everything? Each existing
solution (Polly, HttpClientFactory, Steeltoe Discovery,
IdentityModel, OpenTelemetry) covers one slice; this module wires
them all into one builder so a service that needs to call another
service writes one fluent expression instead of one DI module.

## Mental model

```
                ┌──────────────────────────┐
                │  ServiceClient.<proto>() │
                └──────────────┬───────────┘
                               │
                ┌──────────────┴────────────────┐
                │  fluent builder               │
                │   .WithBaseUrl /              │
                │     .WithEndpointAddress      │
                │   .WithAuth(...)              │
                │   .WithResilience(...)        │
                │   .WithDiscovery(...)         │
                │   .WithLoadBalancer(...)      │
                │   .WithDeduplication(...)     │
                │   .Build()                    │
                └──────────────┬────────────────┘
                               │
                  per-request pipeline:
                               │
              ┌────────────────┼─────────────────┐
              ▼                ▼                 ▼
      ┌──────────────┐  ┌──────────────┐  ┌──────────────┐
      │ deduplicate  │→│ resolve via  │→│ load-balance │
      │ (in-flight)  │  │ discovery    │  │ instance     │
      └──────────────┘  └──────────────┘  └──────────────┘
              │
              ▼
      ┌──────────────┐
      │ resilience   │ → circuit-breaker → retry → timeout
      └──────────────┘
              │
              ▼
      ┌──────────────┐
      │ auth header  │ ← OAuth2 token cache / static bearer / mTLS cert
      └──────────────┘
              │
              ▼
        outbound HTTP / gRPC / WebSocket / SOAP
```

Every concern is opt-in. The minimal builder is just `.WithBaseUrl`
plus `.Build()`; the rest is layered as needed.

## REST

```csharp
using FireflyFramework.Client;
using FireflyFramework.Client.Rest;

var http = ServiceClient.Rest()
    .WithBaseUrl("https://payments.example.com/")
    .WithTimeout(TimeSpan.FromSeconds(10))
    .WithDefaultHeader("X-Tenant", tenantId)
    .WithAuth(a =>
    {
        a.Scheme      = AuthScheme.Bearer;
        a.BearerToken = await tokenSource.GetAsync();
    })
    .WithResilience(r =>
    {
        r.CircuitBreakerEnabled = true;
        r.FailureRateThreshold  = 0.5;
        r.BreakDuration         = TimeSpan.FromSeconds(30);
        r.RetryAttempts         = 3;
    })
    .Build();

// Either use the HttpClient directly:
var resp = await http.GetAsync("/v1/payments/123");

// Or wrap it in a typed client:
IRestClient client = new HttpRestClient(http);
var payment = await client.GetAsync<Payment>("/v1/payments/123");
```

`HttpRestClient` exposes `Task<T?> GetAsync<T>(string)`,
`Task<T?> PostAsync<T>(string, object body)`, `PutAsync`, `PatchAsync`,
and `Task<bool> DeleteAsync(string)`. Bodies are serialised with
System.Text.Json defaults; replace `HttpRestClient` with your own
implementation for custom serialisation.

## SOAP

```csharp
using System.ServiceModel;
using FireflyFramework.Client;
using FireflyFramework.Client.Soap;

[ServiceContract]
public interface IPaymentLegacy
{
    [OperationContract]
    string SubmitPayment(PaymentRequest request);
}

var legacy = ServiceClient.Soap<IPaymentLegacy>()
    .WithEndpointAddress("https://legacy.example.com/payments.svc")
    .WithTransport(SoapTransport.Https)
    .WithBasicAuth("svc-user", "***")
    .WithTimeout(TimeSpan.FromSeconds(30))
    .Build();
```

`Build()` raises `InvalidOperationException` if no endpoint was set.
Internally it uses `BasicHttpBinding` or `BasicHttpsBinding` from
`System.ServiceModel.Http` (WCF Core).

## WebSocket

```csharp
using FireflyFramework.Client;
using FireflyFramework.Client.WebSockets;

await using var ws = ServiceClient.WebSocket();
await ws.ConnectAsync(new Uri("wss://md.svc.local/stream"), ct);
await ws.SendTextAsync("subscribe orders", ct);

await foreach (var frame in ws.ReceiveAsync(ct))
{
    if (frame.Type == System.Net.WebSockets.WebSocketMessageType.Text)
    {
        Console.WriteLine(frame.AsText());
    }
}
```

`WebSocketClientHelper` wraps `System.Net.WebSockets.ClientWebSocket`
with a typed `IAsyncEnumerable<WebSocketFrame>` receive stream and
graceful shutdown via `IAsyncDisposable`.

## gRPC

```csharp
using FireflyFramework.Client;
using FireflyFramework.Client.Grpc;

var channel = ServiceClient.Grpc()
    .WithAddress("https://orders-grpc.example.com")
    .Build();

var client = new Orders.OrdersClient(channel);
var resp = await client.GetOrderAsync(new GetOrderRequest { OrderId = orderId });
```

`Grpc.Net.Client` is the underlying transport; the builder configures
the channel with shared resilience defaults and authentication.

## Auth schemes

| Scheme                     | Behaviour                                                |
|----------------------------|----------------------------------------------------------|
| `None`                     | No auth header set                                       |
| `ApiKey`                   | Adds `<header-name>: <key>` (default header `X-Api-Key`) |
| `Bearer`                   | Adds `Authorization: Bearer <token>`                     |
| `BasicAuth`                | Adds `Authorization: Basic <base64(user:pass)>`          |
| `OAuth2ClientCredentials`  | Token-URL configuration; the application supplies the token via `BearerToken` after acquisition |
| `Mtls`                     | Used by infrastructure to attach a client certificate    |

For OAuth2 client-credentials, pair the auth scheme with the
`OAuth2TokenCache` from `FireflyFramework.Client.OAuth2` so the same
client refreshes the token before expiry rather than fetching one
per call.

## Resilience options

`ClientResilienceOptions` defaults:

| Option                       | Default       |
|------------------------------|---------------|
| `CircuitBreakerEnabled`      | `true`        |
| `FailureRateThreshold`       | `0.5` (50%)   |
| `SlowCallDurationThreshold`  | 2 seconds     |
| `SlidingWindowSize`          | 20            |
| `MinimumThroughput`          | 10            |
| `BreakDuration`              | 30 seconds    |
| `RetryAttempts`              | 3             |
| `RetryBaseDelay`             | 200 ms        |
| `RetryJitter`                | `true`        |

The builder composes a Polly v8 pipeline: circuit breaker →
exponential backoff retry → timeout. The order matters — the breaker
guards the retry, so a tripped circuit short-circuits before
attempting another HTTP call.

## Service discovery

`FireflyFramework.Client.Discovery` ships four clients:

| Client                              | Resolves via                                       |
|-------------------------------------|----------------------------------------------------|
| `StaticServiceDiscoveryClient`      | A configured `Dictionary<string, string[]>`        |
| `EurekaServiceDiscoveryClient`      | Netflix Eureka REST v2 (`/eureka/apps/{name}`)     |
| `ConsulServiceDiscoveryClient`      | HashiCorp Consul REST v1 (`/v1/health/service/...`) |
| `KubernetesServiceDiscoveryClient`  | Kubernetes `Endpoints` API                          |

Wire one with `.WithDiscovery(...)`:

```csharp
var http = ServiceClient.Rest()
    .WithDiscovery(d =>
    {
        d.UseEureka(new Uri("http://eureka-server:8761"));
        d.ServiceName = "payments";
    })
    .Build();
```

The builder resolves the URL per request rather than baking it in,
so an instance change is picked up automatically.

## Load balancing

`FireflyFramework.Client.LoadBalancer` ships four strategies:

| Strategy            | When to use                                              |
|---------------------|----------------------------------------------------------|
| `RoundRobin`        | Default — even spread across instances                   |
| `Random`            | Stateless, lower contention than RoundRobin              |
| `LeastConnections`  | When request durations vary widely                       |
| `WeightedResponse`  | When some instances are larger or geographically closer  |

```csharp
var http = ServiceClient.Rest()
    .WithLoadBalancer(LoadBalancerStrategy.LeastConnections)
    .Build();
```

## OAuth2 token cache

```csharp
var cache = new OAuth2TokenCache(httpClientFactory, options);
var token = await cache.GetTokenAsync(audience: "payments", ct);

// In a builder, plug it into the Bearer auth path:
.WithAuth(async a =>
{
    a.Scheme      = AuthScheme.Bearer;
    a.BearerToken = await cache.GetTokenAsync("payments", ct);
})
```

The cache pre-refreshes the token 30 seconds before expiry so
in-flight calls don't trip on a just-expired token.

## Request deduplication

`FireflyFramework.Client.Deduplication.RequestDeduplicator` collapses
concurrent identical requests into one upstream call:

```csharp
var dedup = new RequestDeduplicator();
var resp = await dedup.DeduplicateAsync(
    key: $"GET:/payments/{id}",
    factory: ct => http.GetAsync($"/payments/{id}", ct));
```

Two callers asking for the same payment at the same time hit the
upstream once. The factory return value is shared across waiters.
Useful for read-heavy endpoints where stale data is fine but extra
upstream load is not.

## Chaos engineering

`FireflyFramework.Client.Chaos.ChaosHandler` injects controlled
failures (latency, errors, timeouts) into the HTTP pipeline so
production resilience can be verified continuously:

```csharp
.WithChaos(c =>
{
    c.LatencyProbability = 0.05;          // 5% of requests get +500ms
    c.LatencyMs          = 500;
    c.ErrorProbability   = 0.01;          // 1% return 503
})
```

Disable in test/debug environments by leaving
`.WithChaos` off; the handler is opt-in.

## Health rollup

`FireflyFramework.Client.Health.HealthRollupService` aggregates the
`/health` endpoints of every registered downstream into a single
verdict — useful for service-mesh dashboards and pre-deploy gates.

## GraphQL helper

`FireflyFramework.Client.GraphQL.GraphQLClient` is a thin layer over
`HttpRestClient`:

```csharp
var graphql = new GraphQLClient(http);
var result = await graphql.QueryAsync<OrdersResponse>(@"
    query { orders { id total status } }
");
```

## Common patterns

### Composing everything in one client

```csharp
var http = ServiceClient.Rest()
    .WithDiscovery(d => d.UseConsul(consulUri).WithName("payments"))
    .WithLoadBalancer(LoadBalancerStrategy.RoundRobin)
    .WithAuth(async a =>
    {
        a.Scheme      = AuthScheme.Bearer;
        a.BearerToken = await tokens.GetTokenAsync("payments", ct);
    })
    .WithResilience(r =>
    {
        r.CircuitBreakerEnabled = true;
        r.RetryAttempts         = 3;
    })
    .WithDeduplication()
    .WithChaos(c => c.LatencyProbability = 0.02)
    .Build();
```

### Per-tenant header

```csharp
.WithDefaultHeader("X-Tenant", () => HttpContextAccessor.HttpContext?.Items["tenant"]?.ToString() ?? "default")
```

The header value is computed per request — no need to rebuild the
client when the tenant changes.

## Pitfalls and gotchas

- **`Build()` is one-shot.** Don't call it per-request; the builder
  composes singletons for the resilience pipeline, the discovery
  client, the load balancer. Build the client once and inject it.
- **Resilience and discovery are independent.** A retry against the
  same instance won't help if that instance is dead. Pair retries
  with discovery + load balancing so a retry rolls to a fresh
  instance.
- **`OAuth2TokenCache` is process-local.** Cross-instance token
  sharing requires Redis (or any `ICacheAdapter`). Without it, every
  pod refreshes independently, which is fine but increases load on
  the IDP.
- **Chaos handler must be off in tests for assertions to be
  deterministic.** It's deliberately probabilistic; pin
  `LatencyProbability = 0` / `ErrorProbability = 0` in tests if you
  build a chaos-enabled client.
- **Deduplication is in-process.** Cross-instance deduplication
  needs a shared cache (use `ICacheAdapter` instead of the in-process
  helper).
- **WebSocket clients must be disposed.** The `await using`
  statement is the right shape; otherwise the socket leaks.

## Internals (for the curious)

- The builder pattern uses `IHttpClientBuilder` under the hood so
  the resilience pipeline is the standard
  `Microsoft.Extensions.Http.Resilience` one — same handler chain
  ASP.NET 10 uses for `AddStandardResilienceHandler()`.
- The OAuth2 token cache uses `SemaphoreSlim` to coalesce concurrent
  refreshes — only one token request goes upstream even if hundreds
  of callers wake up at once.
- The deduplicator is a `ConcurrentDictionary<string, Lazy<Task<T>>>`;
  the `Lazy` ensures the factory runs at most once per key while
  waiters share the same `Task`.
- Service-discovery clients implement a common `IServiceDiscoveryClient`
  interface — swap providers without touching call sites.

## Dependencies

| Reference                                             | Used for           |
|-------------------------------------------------------|--------------------|
| `Polly.Core` + `Microsoft.Extensions.Http.Resilience` | REST resilience    |
| `System.ServiceModel.Http`                            | SOAP / WCF Core    |
| `Grpc.Net.Client`                                     | gRPC channel       |

`System.Net.Http.Json` (used by the typed JSON REST helpers) ships in
the .NET framework — no package import needed.

## Java mapping

| .NET                              | Java                |
|-----------------------------------|---------------------|
| `ServiceClient`                   | `ServiceClient`     |
| `RestClientBuilder`               | `RestClientBuilder` |
| `IRestClient` / `HttpRestClient`  | `RestClient`        |
| `SoapClientBuilder<T>`            | `SoapClientBuilder` |
| `WebSocketClientHelper`           | `WebSocketClientHelper` |
| `GrpcClientBuilder`               | `GrpcClientBuilder` |
| `OAuth2TokenCache`                | `OAuth2TokenCache`  |
| `RequestDeduplicator`             | `RequestDeduplicator` |
| `ChaosHandler`                    | `ChaosFilter`       |
| `HealthRollupService`             | `HealthRollupService` |
| `GraphQLClient`                   | `GraphQLClient`     |
