# FireflyFramework.Webhooks.Sdk

## Overview

`FireflyFramework.Webhooks.Sdk` is a **typed `HttpClient`** for the
webhook-ingestion endpoint exposed by `FireflyFramework.Webhooks.Web`.
Use it from any .NET service that needs to forward an inbound webhook
event into the framework's ingestion pipeline.

The typical use case is split-deployment: a thin "edge" service
receives provider webhooks and forwards them to a dedicated
"webhook processing" service that runs the full pipeline. The edge
service uses this SDK to do the forwarding.

Mirrors `org.fireflyframework:firefly-webhooks-sdk` from the Java
line.

## Why a separate module?

Like every Firefly subsystem's SDK, this module:

- Depends only on `Webhooks.Interfaces` (DTO shapes).
- Brings in `HttpClient` plumbing without ASP.NET / pipeline code.
- Lets a consumer take a 30 KB dependency-free import rather than
  pulling the whole webhook stack.

## Mental model

```
   edge service                                  webhook-processor service
        │                                              ▲
        │ provider hits edge                           │
        │ edge wants to forward                        │
        │                                              │
        ▼                                              │
   IWebhookClient ────── HttpClient ──────► POST /api/webhooks/{provider}
   (this module)                │
                                │
                       message-handler pipeline:
                       ├── correlation-id header
                       ├── auth header
                       ├── Polly retry
                       ├── circuit breaker
                       └── OpenTelemetry span
```

## Quick start

```csharp
using FireflyFramework.Webhooks.Sdk;

builder.Services.AddWebhookClient(new Uri("https://webhooks.svc.local"));
```

Then inject `IWebhookClient` anywhere:

```csharp
public sealed class StripeWebhookForwarder(IWebhookClient client)
{
    public Task<WebhookResponseDto?> Forward(object stripeEvent, CancellationToken ct) =>
        client.SendAsync("stripe", stripeEvent, ct);
}
```

## Public surface

| Member                                        | Calls                                                |
|-----------------------------------------------|------------------------------------------------------|
| `IWebhookClient.SendAsync(provider, payload)` | `POST /api/webhooks/{provider}`                      |
| `AddWebhookClient(IServiceCollection, Uri)`   | Registers `IWebhookClient` + `WebhookClient`         |

`SendAsync` URL-encodes `provider`, posts `payload` as JSON, and
returns the framework's `WebhookResponseDto` (`EventId`, `Status`,
`Message?`, `ProcessingTimeMs`). Non-success responses throw
`HttpRequestException` via `EnsureSuccessStatusCode`.

## Common patterns

### Edge-to-pipeline forwarder

```csharp
[HttpPost("/external/stripe")]
public async Task<IActionResult> StripeEdge(
    [FromServices] IWebhookClient pipeline,
    CancellationToken ct)
{
    using var reader = new StreamReader(Request.Body);
    var payload = JsonDocument.Parse(await reader.ReadToEndAsync(ct)).RootElement;

    var resp = await pipeline.SendAsync("stripe", payload, ct);
    return resp?.Status switch
    {
        "PROCESSED" or "ACCEPTED" => Ok(),
        "RATE_LIMITED"            => StatusCode(429),
        "REJECTED"                => Ok(resp),    // 200 + body, per provider convention
        _                         => StatusCode(500),
    };
}
```

### Adding resilience

```csharp
builder.Services.AddWebhookClient(new Uri("https://webhooks.svc.local"))
    .AddStandardResilienceHandler(o =>
    {
        o.Retry.MaxRetryAttempts = 3;
        o.Retry.Delay            = TimeSpan.FromMilliseconds(200);
        o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
    });
```

Webhook forwarding is naturally idempotent (the pipeline dedup is
your safety net), so retries are cheap. Configure them to be
generous — a brief outage in the pipeline service shouldn't drop
events.

### Forwarding while preserving the original headers

```csharp
public sealed class HeaderForwardingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _ctx;
    public HeaderForwardingHandler(IHttpContextAccessor ctx) => _ctx = ctx;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage req, CancellationToken ct)
    {
        var inbound = _ctx.HttpContext?.Request.Headers;
        if (inbound is null) return base.SendAsync(req, ct);

        foreach (var name in new[] { "Stripe-Signature", "X-Hub-Signature-256" })
        {
            if (inbound.TryGetValue(name, out var v))
                req.Headers.TryAddWithoutValidation(name, v.ToArray());
        }
        return base.SendAsync(req, ct);
    }
}

builder.Services.AddTransient<HeaderForwardingHandler>();
builder.Services.AddWebhookClient(new Uri("https://webhooks.svc.local"))
    .AddHttpMessageHandler<HeaderForwardingHandler>();
```

This lets the downstream pipeline validate signatures using the
original provider-supplied header — without it, the edge service
would have to validate locally and the pipeline would have to trust
a flag.

## Pitfalls and gotchas

- **`EnsureSuccessStatusCode` throws on 4xx as well as 5xx.** A
  rejected event that returns 200 + REJECTED is *not* a 4xx — that
  flow won't throw. But a 429 (rate limited) will throw, even though
  it's a clean rejection. If you want to handle 429 specifically,
  catch the exception and inspect `StatusCode`.
- **The provider name is URL-encoded.** A provider name with
  characters like `:` or `/` works, but stick to ASCII alphanumeric
  for routability.
- **`payload` is serialised via `System.Text.Json`.** Polymorphic or
  custom-serialised payloads need a configured `JsonSerializerOptions`
  — register `IOptions<JsonSerializerOptions>` if your DTOs need
  custom converters.
- **Cancellation tokens are passed through.** A cancelled call
  aborts both the in-flight HTTP request and the pipeline-side
  processing.

## Internals (for the curious)

- `WebhookClient.SendAsync` uses `PostAsJsonAsync` which handles
  serialisation and Content-Type setting in one call.
- `AddWebhookClient` returns `IHttpClientBuilder` so the caller can
  layer additional handlers fluently.

## Dependencies

| Reference                                | Used for                            |
|------------------------------------------|-------------------------------------|
| `FireflyFramework.Webhooks.Interfaces`   | DTO shapes                          |
| `Microsoft.Extensions.Http`              | `AddHttpClient<TClient, TImpl>`     |

`System.Net.Http.Json` ships in the .NET framework — no package
import needed.

## Java mapping

| .NET                  | Java                              |
|-----------------------|-----------------------------------|
| `IWebhookClient`      | `WebhookClient` (interface)       |
| `WebhookClient`       | `WebhookClient`                   |
| `AddWebhookClient`    | Spring Cloud OpenFeign auto-config |
