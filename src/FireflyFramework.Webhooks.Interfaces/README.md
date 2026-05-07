# FireflyFramework.Webhooks.Interfaces

## Overview

`FireflyFramework.Webhooks.Interfaces` is the **public contract
module** for the inbound-webhook subsystem. It is a tiny,
dependency-free assembly that ships only DTOs (record types)
describing the wire format used by the webhook ingestion pipeline.

In Firefly's hub-and-spoke architecture the *interfaces* tier of every
multi-project subsystem plays the same role: it is the only assembly
external callers reference. A consumer that wants to call the
webhook-ingestion REST API (via `Webhooks.Sdk`) brings in this
assembly to deserialize responses and craft requests, but never loads
`Webhooks.Core` (which carries the validators, rate-limiter,
enrichment, and dispatch engine).

Mirrors `org.fireflyframework:firefly-webhooks-interfaces` from the
Java line. The DTO names map one-to-one with the Java records, with
the conventional `Dto` suffix instead of the Java `DTO`.

## Why a separate module?

The full webhook runtime carries non-trivial dependencies — Polly for
retry, the cache abstraction for idempotency, the ASP.NET binding
layer for ingestion. A consumer that simply *forwards* a webhook
payload to the framework over HTTP shouldn't pay for any of that.
Keeping the DTOs in their own dependency-free assembly lets the SDK
and any HTTP client compose with a 30 KB import.

## Public surface

```csharp
public sealed record WebhookEventDto(
    string                         EventId,
    string                         ProviderName,
    JsonElement                    Payload,
    Dictionary<string, string>     Headers,
    Dictionary<string, string>     QueryParams,
    DateTimeOffset                 ReceivedAt,
    string?                        SourceIp,
    string                         HttpMethod,
    Dictionary<string, object?>?   EnrichedMetadata = null);

public sealed record WebhookResponseDto(
    string  EventId,
    string  Status,
    string? Message,
    long    ProcessingTimeMs);
```

### `WebhookEventDto`

| Field              | Notes                                                                  |
|--------------------|------------------------------------------------------------------------|
| `EventId`          | Stable id used for idempotency — typically the provider's own event id |
| `ProviderName`     | Tag identifying the source (`stripe`, `github`, `twilio`, …)           |
| `Payload`          | Raw JSON body as `JsonElement` (no early deserialisation)              |
| `Headers`          | All inbound HTTP headers, lowercased keys                              |
| `QueryParams`      | Query-string parameters                                                |
| `ReceivedAt`       | UTC timestamp set by the ingestion controller                          |
| `SourceIp`         | Caller IP — used by the IP-allow/blacklist validator                   |
| `HttpMethod`       | Always `POST` in practice, but preserved for completeness              |
| `EnrichedMetadata` | Populated by `WebhookMetadataEnrichmentService` (sha256, byte count, transit latency, IP family, user agent) |

### `WebhookResponseDto`

```csharp
public sealed record WebhookResponseDto(
    string  EventId,
    string  Status,
    string? Message,
    long    ProcessingTimeMs);
```

`Status` values used by the framework:

| Value           | Meaning                                                              |
|-----------------|----------------------------------------------------------------------|
| `PROCESSED`     | The processor returned `Success = true`                              |
| `ACCEPTED`      | The processor accepted the event for asynchronous handling          |
| `FAILED`        | Processor returned `Success = false`                                 |
| `REJECTED`      | Validator rejected the event (size, IP, signature, idempotency)      |
| `RATE_LIMITED`  | The rate-limiter denied the event                                    |
| `ERROR`         | An unexpected exception was caught — see `Message` for the cause     |

## Common patterns

### Constructing a `WebhookEventDto` programmatically

```csharp
var evt = new WebhookEventDto(
    EventId:      Guid.NewGuid().ToString(),
    ProviderName: "stripe",
    Payload:      JsonDocument.Parse(rawJson).RootElement,
    Headers:      headers.ToDictionary(h => h.Key.ToLowerInvariant(), h => h.Value),
    QueryParams:  new(),
    ReceivedAt:   DateTimeOffset.UtcNow,
    SourceIp:     ip,
    HttpMethod:   "POST");
```

### Inspecting a response

```csharp
if (response.Status is "REJECTED" or "RATE_LIMITED")
{
    log.LogWarning("Webhook {Id} rejected: {Reason}", response.EventId, response.Message);
    return BadRequest(response);
}
```

## Pitfalls and gotchas

- **`EventId` doubles as the idempotency key.** If you pass a `Guid`
  per call, the idempotency layer can't deduplicate. Use the
  provider's stable id (e.g. Stripe's `evt_*` id, GitHub's
  `X-GitHub-Delivery` header).
- **`Payload` is `JsonElement`.** Don't try to mutate it — it's
  backed by an underlying `JsonDocument` which may be disposed by
  the framework after processing. Read what you need into typed
  records up-front.
- **`Headers` are case-folded by convention.** The framework's
  ingestion controller converts to lowercase. Don't compare with
  case-sensitive equality.
- **Time-stamps are `DateTimeOffset`.** UTC. Always.

## Internals (for the curious)

- `JsonElement` is a struct that holds a reference into a
  `JsonDocument`. The document's lifetime determines when the
  payload bytes are released.
- These records compile down to immutable C# classes with synthesized
  `Equals`, `GetHashCode`, and `Deconstruct`.

## Dependencies

None — pure DTOs. `System.Text.Json.JsonElement` ships in the .NET
framework reference, so no external package is required.

## Java mapping

| .NET                  | Java                              |
|-----------------------|-----------------------------------|
| `WebhookEventDto`     | `WebhookEventDTO`                 |
| `WebhookResponseDto`  | `WebhookResponseDTO`              |
