# FireflyFramework.Webhooks.Core

Inbound-webhook runtime: full processing pipeline (validate, rate-limit,
enrich, dispatch, dead-letter) plus the supporting services for
compression, batching, metadata enrichment, and security validation.

Mirrors `org.fireflyframework:firefly-webhooks-core`.

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
