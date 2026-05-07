# FireflyFramework.Client

Cross-protocol service-client builder for REST, SOAP, WebSocket, and
gRPC. Each protocol has a dedicated fluent builder rooted at
`ServiceClient.{Rest,Soap,WebSocket,Grpc}()`.

Mirrors `org.fireflyframework:firefly-service-client`.

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
with a typed `IAsyncEnumerable<WebSocketFrame>` receive stream.

## gRPC

```csharp
using FireflyFramework.Client;
using FireflyFramework.Client.Grpc;

var channel = ServiceClient.Grpc()
    .WithAddress("https://orders-grpc.example.com")
    .Build();

var client = new Orders.OrdersClient(channel);
```

## Auth schemes

| Scheme                     | Behaviour                                                |
|----------------------------|----------------------------------------------------------|
| `None`                     | No auth header set                                       |
| `ApiKey`                   | Adds `<header-name>: <key>` (default header `X-Api-Key`) |
| `Bearer`                   | Adds `Authorization: Bearer <token>`                     |
| `BasicAuth`                | Adds `Authorization: Basic <base64(user:pass)>`          |
| `OAuth2ClientCredentials`  | Token-URL configuration; the application supplies the token via `BearerToken` after acquisition |
| `Mtls`                     | Used by infrastructure to attach a client certificate    |

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

The builder composes a Polly v8 pipeline: circuit breaker → exponential
backoff retry → timeout.

## Dependencies

| Reference                                             | Used for           |
|-------------------------------------------------------|--------------------|
| `Polly.Core` + `Microsoft.Extensions.Http.Resilience` | REST resilience    |
| `System.ServiceModel.Http`                            | SOAP / WCF Core    |
| `Grpc.Net.Client`                                     | gRPC channel       |

`System.Net.Http.Json` (used by the typed JSON REST helpers) ships in the
.NET 10 framework — no package import needed.

## Java mapping

| .NET                              | Java                |
|-----------------------------------|---------------------|
| `ServiceClient`                   | `ServiceClient`     |
| `RestClientBuilder`               | `RestClientBuilder` |
| `IRestClient` / `HttpRestClient`  | `RestClient`        |
| `SoapClientBuilder<T>`            | `SoapClientBuilder` |
| `WebSocketClientHelper`           | `WebSocketClientHelper` |
| `GrpcClientBuilder`               | `GrpcClientBuilder` |
