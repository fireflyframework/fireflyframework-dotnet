# FireflyFramework.Webhooks.Processor

## Overview

`FireflyFramework.Webhooks.Processor` is the **processor + signature
validators tier** of the inbound-webhook subsystem. It defines:

- The `IWebhookProcessor` port that an application implements to
  handle events for a specific provider.
- Four production-ready signature validators (Stripe, GitHub, Twilio,
  generic HMAC) — every one constant-time-comparing to defeat timing
  attacks.
- The `IWebhookIdempotencyService` SPI for per-`(eventId, provider)`
  deduplication, with a default cache-backed implementation.

Mirrors `org.fireflyframework:firefly-webhooks-processor` from the
Java line. The validator behaviour and signature spellings match
exactly so a migration from Java to .NET (or vice-versa) doesn't
require re-issuing webhook secrets.

## Why a separate module?

Signature validation and processor implementation are *application*
concerns — they speak the domain. Keeping them in their own assembly:

- Lets a service depend on `Webhooks.Processor` for validators
  without taking the full pipeline (`Webhooks.Core`).
- Lets a deployment that runs the pipeline as a remote service still
  validate signatures locally (e.g. at the edge) before forwarding.
- Mirrors the Java line's modular packaging.

## Mental model

```
   inbound webhook event
        │
        ▼
   ┌──────────────────────────┐
   │ IWebhookSignatureValidator│  Stripe / GitHub / Twilio / generic HMAC
   │ (constant-time compare)  │
   └──────────┬───────────────┘
              │
              ▼
   ┌──────────────────────────┐
   │ IWebhookIdempotency      │  TryAcquireAsync(eventId, provider, ttl)
   │ Service                  │  Cache-based (Redis / Memory)
   └──────────┬───────────────┘
              │ first time only
              ▼
   ┌──────────────────────────┐
   │ IWebhookProcessor        │  Application-supplied
   │   BeforeProcessAsync     │
   │   ProcessAsync           │  ← does the actual work
   │   AfterProcessAsync      │
   │   OnErrorAsync           │
   └──────────────────────────┘
```

The order matters. **Validate signature first** (don't waste
resources on attacker traffic), **then check idempotency**, **then
process**. The framework's pipeline (`Webhooks.Core`) calls these in
the right order.

## Signature validators

All four validators implement `IWebhookSignatureValidator` and use
`CryptographicOperations.FixedTimeEquals` to defeat timing attacks.

### Stripe

```csharp
var validator = new StripeSignatureValidator(tolerance: TimeSpan.FromMinutes(5));
var ok = await validator.ValidateSignatureAsync(payload, headers, secret);
```

Reads `Stripe-Signature` (e.g. `t=1700000000,v1=hex`), verifies
`HMAC-SHA256({timestamp}.{rawPayload}, secret)`, and rejects
timestamps outside the tolerance window.

The tolerance window is critical: too narrow and clock skew between
your service and Stripe causes false rejections; too wide and
captured signatures stay valid for replay. Stripe's recommended
default is 5 minutes — this validator's default.

### GitHub

Reads `X-Hub-Signature-256` (e.g. `sha256=hex`), verifies
`HMAC-SHA256(rawPayload, secret)`. GitHub doesn't include a timestamp,
so replay protection comes from the idempotency layer (key on
`X-GitHub-Delivery`).

### Twilio

Reads `X-Twilio-Signature`, verifies `HMAC-SHA1` of the absolute URL
followed by sorted form parameters, base64-encoded. The caller is
responsible for constructing the canonicalised string and passing it
as the `payload` argument:

```csharp
var url    = $"{Request.Scheme}://{Request.Host}{Request.Path}";
var fields = string.Concat(formParams.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                                     .Select(kv => kv.Key + kv.Value));
var ok = await twilio.ValidateSignatureAsync(url + fields, headers, secret);
```

This is awkward but matches Twilio's own validator — match it
exactly or signatures will mysteriously fail.

### Generic HMAC

```csharp
var validator = new HmacSignatureValidator(
    headerName: "X-Signature",
    algorithm:  HmacAlgorithm.Sha256,
    prefix:     "sha256=");   // optional
```

Supports `Sha1`, `Sha256`, and `Sha512`. Optionally strips a fixed
prefix from the header value before comparison. Use it for any
provider whose signature scheme is plain `HMAC(payload, secret)` —
GitLab, Slack, Bitbucket, custom in-house webhooks.

## Processor port

```csharp
public interface IWebhookProcessor
{
    Task<WebhookProcessingResult> ProcessAsync(
        WebhookProcessingContext context, CancellationToken ct = default);

    // Default no-op hooks the application can override:
    Task BeforeProcessAsync(WebhookProcessingContext context, CancellationToken ct = default);
    Task AfterProcessAsync (WebhookProcessingContext context,
                            WebhookProcessingResult result,
                            CancellationToken ct = default);
    Task OnErrorAsync      (WebhookProcessingContext context,
                            Exception error,
                            CancellationToken ct = default);
}

public sealed record WebhookProcessingContext(WebhookEventDto Event, string ProviderName);
public sealed record WebhookProcessingResult (bool Success, bool ShouldRetry, TimeSpan? RetryAfter, string? Message);
```

The hook methods are *no-ops by default* on a base class —
override them when you need cross-cutting work (logging,
auditing, span tagging) without polluting `ProcessAsync`.

## Idempotency

`IWebhookIdempotencyService.TryAcquireAsync(eventId, provider, ttl)`
returns `true` only the first time it is called for a given
`(eventId, provider)` pair within the supplied TTL window. The
default implementation `CacheBasedWebhookIdempotencyService` uses
`ICacheAdapter` so it works against Redis or in-memory.

```csharp
public async Task<WebhookProcessingResult> ProcessAsync(
    WebhookProcessingContext ctx, CancellationToken ct)
{
    var fresh = await idempotency.TryAcquireAsync(
        ctx.Event.EventId, ctx.ProviderName,
        ttl: TimeSpan.FromHours(24), ct);

    if (!fresh) return WebhookProcessingResult.Skipped();   // already seen

    // ... actual work
    return WebhookProcessingResult.Ok();
}
```

Pick `ttl` ≥ the provider's longest retry window: Stripe retries up
to 3 days; GitHub retries up to 8 hours.

## Common patterns

### Stripe processor with signature + idempotency

```csharp
public sealed class StripeWebhookProcessor(
    IWebhookSignatureValidator signature,
    IWebhookIdempotencyService idempotency,
    string                     stripeSecret,
    IOrderRepository           orders) : IWebhookProcessor
{
    public async Task<WebhookProcessingResult> ProcessAsync(
        WebhookProcessingContext ctx, CancellationToken ct)
    {
        var ok = await signature.ValidateSignatureAsync(
            ctx.Event.Payload.ToString()!, ctx.Event.Headers, stripeSecret);
        if (!ok) return WebhookProcessingResult.Failed("Bad signature", retryable: false);

        var fresh = await idempotency.TryAcquireAsync(
            ctx.Event.EventId, ctx.ProviderName, TimeSpan.FromDays(7), ct);
        if (!fresh) return WebhookProcessingResult.Skipped();

        var type = ctx.Event.Payload.GetProperty("type").GetString();
        if (type == "checkout.session.completed")
        {
            var orderId = ctx.Event.Payload
                .GetProperty("data").GetProperty("object")
                .GetProperty("metadata").GetProperty("orderId").GetString()!;
            await orders.MarkPaidAsync(Guid.Parse(orderId), ct);
        }
        return WebhookProcessingResult.Ok();
    }
}
```

## Pitfalls and gotchas

- **Always use `FixedTimeEquals`.** A naive `string.Equals` leaks
  byte-by-byte timing information. The framework's validators do
  this for you — don't roll your own comparison.
- **Twilio canonicalisation is finicky.** The signed string is
  *URL + sorted form params concatenated*. Matching this exactly
  requires preserving the request's `Host` header (proxies sometimes
  rewrite it) and sorting form keys in ordinal (not culture-aware)
  order.
- **GitHub uses both SHA-1 (legacy) and SHA-256 (current) headers.**
  Always validate the SHA-256 header; ignore SHA-1 for new
  integrations.
- **Idempotency TTL is the *deduplication* window, not the *retry*
  window.** Set it to the provider's longest retry window, not your
  application's processing time.
- **Don't trust `EventId` to be unique across providers.** The key
  is `(eventId, provider)` — Stripe and GitHub may both emit
  `evt_123abc`.

## Internals (for the curious)

- `StripeSignatureValidator.ValidateSignatureAsync` parses the
  `Stripe-Signature` header into `{t=..., v1=...}` pairs, computes
  `HMAC-SHA256("t.payload", secret)`, and `FixedTimeEquals` against
  the supplied `v1`.
- `HmacSignatureValidator` uses `HMACSHA256.HashData` (or sha1/sha512
  equivalents) which is allocation-free for typical secret sizes.
- `CacheBasedWebhookIdempotencyService` uses `PutIfAbsentAsync` from
  `ICacheAdapter` — that's the SETNX-style operation that returns
  `true` only if the key didn't exist. The TTL is set on the same
  call so the lock auto-expires.

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
