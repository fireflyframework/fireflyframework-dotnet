# FireflyFramework.Webhooks.Processor

Signature validators, idempotency, and the per-provider processor port
for inbound webhooks. Use these primitives to authenticate, deduplicate,
and route incoming events.

Mirrors `org.fireflyframework:firefly-webhooks-processor`.

## Signature validators

All four validators implement `IWebhookSignatureValidator` and use
`CryptographicOperations.FixedTimeEquals` to defeat timing attacks.

### Stripe

```csharp
var validator = new StripeSignatureValidator(tolerance: TimeSpan.FromMinutes(5));
var ok = await validator.ValidateSignatureAsync(payload, headers, secret);
```

Reads `Stripe-Signature` (e.g. `t=1700000000,v1=hex`), verifies
`HMAC-SHA256({timestamp}.{rawPayload}, secret)`, and rejects timestamps
outside the tolerance window.

### GitHub

Reads `X-Hub-Signature-256` (e.g. `sha256=hex`), verifies
`HMAC-SHA256(rawPayload, secret)`.

### Twilio

Reads `X-Twilio-Signature`, verifies `HMAC-SHA1` of the absolute URL
followed by sorted form parameters, base64-encoded. The caller is
responsible for constructing the canonicalised string and passing it as
the `payload` argument.

### Generic HMAC

```csharp
var validator = new HmacSignatureValidator(
    headerName: "X-Signature",
    algorithm:  HmacAlgorithm.Sha256,
    prefix:     "sha256=");   // optional
```

Supports `Sha1`, `Sha256`, and `Sha512`. Optionally strips a fixed
prefix from the header value before comparison.

## Processor port

```csharp
public interface IWebhookProcessor
{
    Task<WebhookProcessingResult> ProcessAsync(WebhookProcessingContext context, CancellationToken ct = default);

    // Default no-op hooks the application can override:
    Task BeforeProcessAsync(WebhookProcessingContext context, CancellationToken ct = default);
    Task AfterProcessAsync (WebhookProcessingContext context, WebhookProcessingResult result, CancellationToken ct = default);
    Task OnErrorAsync      (WebhookProcessingContext context, Exception error, CancellationToken ct = default);
}

public sealed record WebhookProcessingContext(WebhookEventDto Event, string ProviderName);
public sealed record WebhookProcessingResult (bool Success, bool ShouldRetry, TimeSpan? RetryAfter, string? Message);
```

## Idempotency

`IWebhookIdempotencyService.TryAcquireAsync(eventId, provider, ttl)`
returns `true` only the first time it is called for a given
`(eventId, provider)` pair within the supplied TTL window. The default
implementation `CacheBasedWebhookIdempotencyService` uses
`ICacheAdapter` so it works against Redis or in-memory.

## Dependencies

| Reference                                | Used for                       |
|------------------------------------------|--------------------------------|
| `FireflyFramework.Webhooks.Interfaces`   | DTOs                           |
| `FireflyFramework.Cache`                 | Idempotency store              |

## Java mapping

| .NET                                | Java                                  |
|-------------------------------------|---------------------------------------|
| `IWebhookSignatureValidator`        | `WebhookSignatureValidator`           |
| `StripeSignatureValidator`          | `StripeSignatureValidator`            |
| `GitHubSignatureValidator`          | `GitHubSignatureValidator`            |
| `TwilioSignatureValidator`          | `TwilioSignatureValidator`            |
| `HmacSignatureValidator`            | `HmacSignatureValidator`              |
| `IWebhookProcessor`                 | `WebhookProcessor`                    |
| `IWebhookIdempotencyService`        | `WebhookIdempotencyService`           |
