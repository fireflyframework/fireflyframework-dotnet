# FireflyFramework.Webhooks.Web

## Overview

`FireflyFramework.Webhooks.Web` is the **HTTP ingestion controller**
for inbound webhooks. It receives the raw provider payload, builds a
`WebhookEventDto`, and dispatches it to `IWebhookProcessingService`
from `Webhooks.Core` for the full validate → rate-limit → enrich →
dispatch → DLQ pipeline.

The controller is the only piece in this assembly. Everything else
lives in `Webhooks.Core`, so an in-process consumer can drive the
pipeline without taking the ASP.NET binding layer.

Mirrors `org.fireflyframework:firefly-webhooks-web`. The route
template, payload binding, and response shape match the Java line
exactly.

## Why a separate module?

Two reasons keep the controller separate:

1. **In-process consumers.** A test harness or a service that
   receives webhook events over a queue rather than HTTP doesn't need
   ASP.NET — it can call `IWebhookProcessingService` directly.
2. **Custom hosting.** Teams that mount webhooks under a non-default
   prefix, behind a custom auth layer, or with their own request
   validation pipeline can subclass the controller without forking
   the rest of the stack.

## Mental model

```
   provider (Stripe, GitHub, Twilio, …)
        │
        │  POST /api/webhooks/{provider}
        │  Content-Type: application/json
        │  X-Stripe-Signature / X-Hub-Signature-256 / etc.
        ▼
   ┌─────────────────────────────────────┐
   │  WebhookController                  │
   │   - reads raw body                  │
   │   - builds WebhookEventDto          │
   │   - forwards to processing service  │
   └────────────┬────────────────────────┘
                │
                ▼
   ┌─────────────────────────────────────┐
   │  IWebhookProcessingService          │
   │   (Webhooks.Core)                   │
   └─────────────────────────────────────┘
                │
                ▼
   200 OK { eventId, status, ... }
```

The controller is intentionally thin — it doesn't validate
signatures, deduplicate, or transform. Those are the pipeline's job.

## Endpoint

| Method | Path                                | Body          | Description                                  |
|--------|-------------------------------------|---------------|----------------------------------------------|
| POST   | `/api/webhooks/{provider}`          | JSON object   | Ingest a webhook event from `{provider}`     |

The controller forwards the raw body, headers, query string, source
IP, and HTTP method into a `WebhookEventDto` and returns the
`WebhookResponseDto` produced by the pipeline (`EventId`, `Status`,
`Message?`, `ProcessingTimeMs`).

### Response codes

| HTTP status | When                                                           |
|-------------|----------------------------------------------------------------|
| 200         | `Status` ∈ `{ PROCESSED, ACCEPTED, FAILED, REJECTED }` — the event reached the pipeline |
| 429         | `Status = RATE_LIMITED`                                         |
| 500         | `Status = ERROR` — unexpected exception                         |

A 200 with `Status = REJECTED` is *not* an error — it means the
pipeline cleanly rejected the event (signature bad, IP not allowed,
etc.). Treat it as "received but not processed."

## Wiring

```csharp
using FireflyFramework.Webhooks.Core;
using FireflyFramework.Webhooks.Web;

builder.Services.AddSingleton<IWebhookProcessingService, WebhookProcessingService>();
builder.Services
    .AddControllers()
    .AddApplicationPart(typeof(WebhookController).Assembly);
```

`AddApplicationPart` exposes the controller — without it ASP.NET
only scans the host assembly.

## Common patterns

### Mounting under a non-default prefix

```csharp
[Route("v2/hooks")]
public sealed class V2WebhookController : WebhookController
{
    public V2WebhookController(IWebhookProcessingService s) : base(s) { }
}
```

### Adding request-rate limit at the edge

The pipeline's rate-limiter is *per-provider*. To protect against a
single misbehaving caller, layer ASP.NET's per-IP rate-limit
middleware before the controller:

```csharp
app.UseRateLimiter();
app.MapControllers();
```

### Logging the source IP

The controller copies `HttpContext.Connection.RemoteIpAddress` into
`WebhookEventDto.SourceIp`. If you're behind a load balancer, enable
`UseForwardedHeaders` so the real client IP shows up:

```csharp
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor,
});
```

## Pitfalls and gotchas

- **The path prefix is `/api/webhooks/{provider}`.** Anything else
  is ignored. Mount your reverse proxy accordingly.
- **The body must be JSON.** Form-urlencoded providers (Twilio sends
  application/x-www-form-urlencoded) are not directly supported by
  this controller — receive them via a custom controller that
  re-shapes the body into JSON, then call
  `IWebhookProcessingService` directly.
- **Headers are passed through case-folded.** Most providers' header
  names are case-insensitive HTTP-by-spec; the controller folds to
  lowercase to keep downstream code simple.
- **Response is JSON regardless of `Accept`.** The controller doesn't
  honour content negotiation — webhook callers don't read the
  response body anyway.
- **Cancellation respected.** A long-running pipeline call is
  cancelled when the client disconnects. This matters during a
  burst — give up early on dead connections.

## Internals (for the curious)

- The controller has zero injected services beyond
  `IWebhookProcessingService`. Adding more would duplicate concerns
  the pipeline already owns (logging, metrics, dead-letter).
- `[FromRoute] string provider` is URL-decoded by ASP.NET, so a
  provider name with special characters (rare but legal) works
  out of the box.
- Setting `EnableBuffering()` on the request body is *not* needed
  because the controller reads the body once into a string and
  hands it to the DTO.

## Dependencies

| Reference                                | Used for                            |
|------------------------------------------|-------------------------------------|
| `FireflyFramework.Webhooks.Core`         | `IWebhookProcessingService`         |
| `FireflyFramework.Webhooks.Interfaces`   | DTOs                                |
| `Microsoft.AspNetCore.App`               | `[ApiController]`, MVC binding      |

## Java mapping

| .NET                  | Java                              |
|-----------------------|-----------------------------------|
| `WebhookController`   | `WebhookController`               |
