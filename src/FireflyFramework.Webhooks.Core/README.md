# FireflyFramework.Webhooks.Core

## Overview

`FireflyFramework.Webhooks.Core` is the **inbound-webhook runtime
tier**. It ships the full processing pipeline (validate → rate-limit
→ enrich → dispatch → dead-letter) plus the supporting services for
compression, batching, metadata enrichment, and security validation.

The pipeline is deliberately step-by-step so each stage can be
overridden, replaced, or instrumented independently. Most production
deployments use the defaults end-to-end and customise only the
processor (which speaks the application's domain) and the rate-limit
configuration.

Mirrors `org.fireflyframework:firefly-webhooks-core`. The processing
contract, rate-limiter behaviour, and dead-letter shape match the
Java line — a hybrid Java/.NET deployment can route webhooks to
either runtime without semantic change.

## Why a separate module?

The webhook surface has three independent personas:

- **The HTTP ingestion controller** (in `Webhooks.Web`) — receives
  raw bytes from the provider.
- **The pipeline runtime** (this module) — validates, rate-limits,
  enriches, and dispatches.
- **The processor** (provided by your service) — speaks the
  application's domain.

Splitting them lets you deploy a webhook receiver as a separate
service (web + core, no processor — just dispatch the event onto
Kafka) or as part of a monolith (all three together), without
rewiring code.

## Mental model

```
   inbound HTTP
        │
        ▼
   ┌──────────────────────────┐
   │ WebhookController        │  (in Webhooks.Web)
   │ builds WebhookEventDto   │
   └──────────┬───────────────┘
              │
              ▼
   ┌──────────────────────────┐
   │ WebhookProcessingService │
   │   1. Validate            │  (size, IP allow/block)
   │   2. Rate-limit          │  (token bucket per provider)
   │   3. Enrich              │  (sha256, latency, ip family)
   │   4. Dispatch            │  → IWebhookProcessor (your code)
   │   5. Dead-letter on fail │
   └──────────┬───────────────┘
              │
              ▼
   ┌──────────────────────────┐
   │ Your IWebhookProcessor   │
   │ (in Webhooks.Processor)  │
   └──────────────────────────┘
```

Each pipeline step short-circuits on failure: a rejected event never
reaches the rate-limiter; a rate-limited event never reaches the
enricher. The response carries the status that explains *which*
step said no.

## Pipeline

`WebhookProcessingService` runs the following pipeline for every
inbound event:

1. **Validate** (`WebhookValidator`) — payload size limit, optional
   IP allow/blacklist (CIDR-aware).
2. **Rate-limit** (`WebhookRateLimitService`) — token-bucket per
   provider; in-process by default, Redis-backed when an
   `ICacheAdapter` is provided.
3. **Enrich** (`WebhookMetadataEnrichmentService`) — adds payload
   SHA-256 + byte count, transit latency from `x-event-timestamp`,
   IP family, user agent.
4. **Dispatch** to a registered `IWebhookProcessor` looked up by
   provider name.
5. **Dead-letter** on processor failure (`IDeadLetterQueueService`).

```csharp
var response = await processingService.ProcessAsync(evt, ct);
// response.Status is one of PROCESSED / ACCEPTED / FAILED / REJECTED / RATE_LIMITED / ERROR
```

## Public surface

### Processing

| Type                                | Purpose                                                              |
|-------------------------------------|----------------------------------------------------------------------|
| `IWebhookProcessingService`         | Entry point: `Task<WebhookResponseDto> ProcessAsync(WebhookEventDto)` |
| `WebhookProcessingService`          | Default pipeline implementation                                      |
| `IWebhookProcessorRegistry`         | Maps `providerName` to `IWebhookProcessor`                           |
| `InMemoryWebhookProcessorRegistry`  | Default in-process registry                                          |

### Validation

| Type                                | Purpose                                                              |
|-------------------------------------|----------------------------------------------------------------------|
| `WebhookValidator`                  | Size + IP CIDR check                                                 |
| `WebhookSecurityOptions`            | `IpWhitelist`, `IpBlacklist`, `RequireSignature`, `MaxPayloadBytes`  |
| `WebhookValidationResult`           | `IsValid`, `Reason`                                                  |
| `WebhookValidator.InCidr`           | Static helper for IPv4 / IPv6 CIDR matching                          |

### Rate limit

| Type                                | Purpose                                                              |
|-------------------------------------|----------------------------------------------------------------------|
| `WebhookRateLimitService`           | Per-provider rate limiter                                            |
| `RateLimitOptionsConfiguration`     | `Enabled`, `RequestsPerSecond`, `BurstSize`, `Window`                |

The rate-limiter uses a token-bucket algorithm: every provider has a
bucket of `BurstSize` tokens; each event consumes one; tokens refill
at `RequestsPerSecond`. Out of tokens → `RATE_LIMITED`.

### Compression / Batching / Enrichment

| Type                                | Purpose                                                              |
|-------------------------------------|----------------------------------------------------------------------|
| `WebhookCompressionService`         | GZIP compress / decompress with min-size threshold                   |
| `CompressionOptions`                | `Enabled`, `MinSizeBytes`                                            |
| `WebhookBatchingService`            | `Channel<T>`-based micro-batching                                    |
| `BatchingOptions`                   | `Enabled`, `BatchSize`, `BatchWindow`, `MaxAge`                      |
| `WebhookMetadataEnrichmentService`  | Adds `payloadSha256`, `payloadBytes`, `transitLatencyMs`, `ipFamily`, `isLoopback`, `userAgent` |

### Dead-letter queue

| Type                                | Purpose                                                              |
|-------------------------------------|----------------------------------------------------------------------|
| `IDeadLetterQueueService`           | Publish, list, redrive, remove                                       |
| `InMemoryDeadLetterQueueService`    | Default in-process implementation                                    |
| `DeadLetterEntry`                   | `Id`, `Provider`, `EventId`, `Event`, `Reason`, `Attempts`, `DeadLetteredAt` |

## Configuration

```json
{
  "Firefly": {
    "Webhooks": {
      "Security": {
        "IpWhitelist":      [ "192.0.2.0/24" ],
        "IpBlacklist":      [ ],
        "RequireSignature": true,
        "MaxPayloadBytes":  1048576
      },
      "RateLimit": {
        "Enabled":             true,
        "RequestsPerSecond":   100,
        "BurstSize":           500,
        "Window":              "00:00:01"
      },
      "Compression": { "Enabled": true, "MinSizeBytes": 1024 },
      "Batching":    { "Enabled": false, "BatchSize": 100, "BatchWindow": "00:00:00.500" }
    }
  }
}
```

## Common patterns

### Implementing a processor

```csharp
public sealed class StripeWebhookProcessor(IOrderRepository orders) : IWebhookProcessor
{
    public async Task<WebhookProcessingResult> ProcessAsync(
        WebhookProcessingContext ctx, CancellationToken ct)
    {
        var type = ctx.Event.Payload.GetProperty("type").GetString();
        if (type != "checkout.session.completed") return WebhookProcessingResult.Skipped();

        var orderId = ctx.Event.Payload
            .GetProperty("data").GetProperty("object")
            .GetProperty("metadata").GetProperty("orderId").GetString()!;

        await orders.MarkPaidAsync(Guid.Parse(orderId), ct);
        return WebhookProcessingResult.Ok();
    }
}

services.AddSingleton<IWebhookProcessor, StripeWebhookProcessor>();
services.AddSingleton<IWebhookProcessorRegistry>(sp =>
{
    var registry = new InMemoryWebhookProcessorRegistry();
    registry.Register("stripe", sp.GetRequiredService<StripeWebhookProcessor>());
    return registry;
});
```

### Failing into the dead-letter queue

```csharp
public Task<WebhookProcessingResult> ProcessAsync(WebhookProcessingContext ctx, CancellationToken ct)
{
    try
    {
        // ... handle event
        return Task.FromResult(WebhookProcessingResult.Ok());
    }
    catch (UpstreamUnavailableException ex)
    {
        return Task.FromResult(new WebhookProcessingResult(
            Success:     false,
            ShouldRetry: true,
            RetryAfter:  TimeSpan.FromMinutes(1),
            Message:     ex.Message));
    }
    catch (Exception ex)
    {
        return Task.FromResult(new WebhookProcessingResult(
            Success: false, ShouldRetry: false, RetryAfter: null, Message: ex.Message));
    }
}
```

`ShouldRetry: false` sends the event to the DLQ. `ShouldRetry: true`
re-enqueues for processing after `RetryAfter`.

### Inspecting and redriving the DLQ

```csharp
[HttpGet("/admin/webhooks/dead-letter")]
public Task<IReadOnlyList<DeadLetterEntry>> List(IDeadLetterQueueService dlq, CancellationToken ct) =>
    dlq.ListAsync(provider: null, limit: 100, ct);

[HttpPost("/admin/webhooks/dead-letter/{id}/redrive")]
public async Task<WebhookResponseDto> Redrive(
    Guid id,
    IDeadLetterQueueService dlq,
    IWebhookProcessingService pipeline,
    CancellationToken ct)
{
    var entry = await dlq.GetAsync(id, ct);
    if (entry is null) return new(id.ToString(), "REJECTED", "Not found", 0);

    var resp = await pipeline.ProcessAsync(entry.Event, ct);
    if (resp.Status is "PROCESSED" or "ACCEPTED")
        await dlq.RemoveAsync(id, ct);
    return resp;
}
```

## Pitfalls and gotchas

- **The pipeline is single-pass.** A rejected event is *not*
  retried. Failed events go to the DLQ; the operator must redrive
  manually (or schedule a job).
- **Rate-limit is per provider, not per IP.** A misbehaving caller
  using a popular provider name (`stripe`) will trip the rate-limit
  for *legitimate* Stripe traffic. Pair with IP allowlisting on the
  edge.
- **Idempotency belongs to the processor.** The pipeline doesn't
  deduplicate — `IWebhookIdempotencyService` (in `Webhooks.Processor`)
  is your tool for that, called from inside `ProcessAsync`.
- **`MaxPayloadBytes` is enforced *after* full body read.** The
  ingestion controller buffers the body; for very large payloads,
  add a layer-7 limit at the load balancer.
- **`RequireSignature` is advisory.** The pipeline records that a
  signature is required, but it doesn't *verify* — that's the
  processor's job using `IWebhookSignatureValidator`. The flag
  exists so processors can fail-fast when configured to.
- **Compression is for storage, not transport.** The
  `WebhookCompressionService` compresses payloads when persisting to
  the DLQ; HTTP transport compression is the load balancer's job.

## Internals (for the curious)

- `WebhookRateLimitService` uses a `ConcurrentDictionary<string, TokenBucket>`
  keyed on provider name. Eviction is opportunistic — buckets that
  haven't seen traffic in `Window * 10` are dropped on next read.
- The enrichment service hashes the payload *exactly once* and
  stashes the result in `EnrichedMetadata` so downstream stages
  don't recompute.
- The DLQ uses `WebhookCompressionService` for entries above
  `MinSizeBytes` (default 1 KB) so a flood of large rejected events
  doesn't blow the in-memory store.

## Dependencies

| Reference                              | Used for                       |
|----------------------------------------|--------------------------------|
| `FireflyFramework.Webhooks.Interfaces` | DTOs                           |
| `FireflyFramework.Webhooks.Processor`  | `IWebhookProcessor`            |
| `FireflyFramework.Cache`               | Distributed rate-limiter store |
| `FireflyFramework.Eda`                 | Optional event publication     |

## Java mapping

| .NET                                | Java                                                |
|-------------------------------------|-----------------------------------------------------|
| `WebhookProcessingService`          | `WebhookProcessingService`                          |
| `WebhookValidator`                  | `WebhookValidator`                                  |
| `WebhookRateLimitService`           | `WebhookRateLimitService`                           |
| `WebhookCompressionService`         | `WebhookCompressionService`                         |
| `WebhookBatchingService`            | `WebhookBatchingService`                            |
| `WebhookMetadataEnrichmentService`  | `WebhookMetadataEnrichmentService`                  |
| `IDeadLetterQueueService`           | `DeadLetterQueueHandler` + `DeadLetterQueueEvent`   |
