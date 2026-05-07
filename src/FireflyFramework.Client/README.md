# FireflyFramework.Client

Fluent service-client builder for REST and gRPC with Polly v8 resilience (circuit breaker, retry, timeout) and pluggable auth schemes. Mirrors `fireflyframework-client`.

## Quick start (REST)

```csharp
var http = ServiceClient.Rest()
    .WithBaseUrl("https://payments.example.com/")
    .WithTimeout(TimeSpan.FromSeconds(10))
    .WithDefaultHeader("X-Tenant", tenantId)
    .WithAuth(a =>
    {
        a.Scheme = AuthScheme.Bearer;
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

var response = await http.GetAsync("/v1/payments/123");
```

## Quick start (gRPC)

```csharp
var channel = ServiceClient.Grpc()
    .WithAddress("https://orders-grpc.example.com")
    .Build();

var client = new Orders.OrdersClient(channel);
```

## Auth schemes

| Scheme | Notes |
|---|---|
| `None` | No auth |
| `ApiKey` | `X-Api-Key` (configurable header name) |
| `Bearer` | `Authorization: Bearer …` |
| `BasicAuth` | Base64 username:password |
| `OAuth2ClientCredentials` | Token URL, client id/secret, scope (token cache TODO) |
| `Mtls` | mTLS via `ClientCertPath` + `ClientCertPassword` (handler integration TODO) |

## Resilience

The builder composes a Polly v8 pipeline: circuit breaker → exponential-backoff retry (with optional jitter) → timeout. Override defaults via `WithResilience(r => …)`.
