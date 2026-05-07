# FireflyFramework.Ecm.ESignature.AdobeSign

## Overview

`FireflyFramework.Ecm.ESignature.AdobeSign` is the **Adobe Sign
implementation of `ISignatureEnvelopePort`**. It calls the Adobe Sign
REST v6 agreements API with OAuth2 refresh-token authentication.

Mirrors `org.fireflyframework:firefly-ecm-esignature-adobe-sign`.
The endpoint paths, payload shapes, and refresh-token flow match the
Java line.

## Why a separate module?

Adobe Sign and DocuSign are mutually exclusive in most deployments —
a service uses one or the other. Splitting the adapters per-vendor:

- Lets the consumer bring in only the SDKs / dependencies it actually
  needs.
- Lets each adapter expose vendor-specific extension methods without
  cluttering the port surface.
- Mirrors the Java line's modular packaging.

## Adapter type

```csharp
[EcmAdapter("adobe-sign",
    Description       = "Adobe Sign Envelope Adapter",
    SupportedFeatures = AdapterFeature.ESignatureEnvelopes
                      | AdapterFeature.ESignatureRequests,
    RequiredProperties = new[] { "ClientId", "ClientSecret", "RefreshToken" })]
public sealed class AdobeSignSignatureEnvelopeAdapter : ISignatureEnvelopePort { ... }
```

## Mental model

```
   application code
        │
        │  ISignatureEnvelopePort.CreateEnvelopeAsync(...)
        ▼
   ┌──────────────────────────┐
   │ AdobeSignSignatureEnv.   │
   │ Adapter                  │
   └──────────┬───────────────┘
              │
              │ refresh-token exchange
              ▼
   ┌──────────────────────────┐
   │ Adobe Sign OAuth2        │
   │ POST /oauth/refresh      │
   └──────────┬───────────────┘
              │ access token
              ▼
   ┌──────────────────────────┐
   │ Adobe Sign REST v6       │
   │ /api/rest/v6/agreements  │
   └──────────────────────────┘
```

The refresh-token flow runs once per token lifetime and the cached
access token is reused for envelope operations until expiry.

## Configuration

```json
{
  "Firefly": {
    "Ecm": {
      "ESignature": {
        "AdobeSign": {
          "BaseUrl":      "https://api.eu1.adobesign.com",
          "ClientId":     "<application id>",
          "ClientSecret": "<application secret>",
          "RefreshToken": "<offline-access refresh token>"
        }
      }
    }
  }
}
```

| Property        | Notes                                                                  |
|-----------------|------------------------------------------------------------------------|
| `BaseUrl`       | Region-specific (e.g. `api.eu1.adobesign.com`, `api.na2.adobesign.com`) — ask the Adobe console which one your account is on |
| `ClientId`      | Application id from the integration's app registration                 |
| `ClientSecret`  | Application secret                                                     |
| `RefreshToken`  | Offline-access refresh token; obtained via the consent flow once       |

The adapter exchanges the refresh token for an access token on demand
and caches it in memory until expiry.

## Wiring

```csharp
builder.Services.Configure<AdobeSignOptions>(builder.Configuration.GetSection(AdobeSignOptions.SectionName));
builder.Services.AddHttpClient<AdobeSignSignatureEnvelopeAdapter>();
```

The typed `HttpClient` registration brings in the standard pipeline
(handler reuse, retry, logging) so you can layer
`AddStandardResilienceHandler()` on top.

## Common patterns

### Creating and sending an agreement

```csharp
var envelope = await sign.CreateEnvelopeAsync(new SignatureEnvelope
{
    Subject = "Please sign your contract",
    Message = "Hi, please review and sign by Friday.",
    Documents = new[]
    {
        new SignatureDocument
        {
            Name        = "contract.pdf",
            ContentType = "application/pdf",
            Content     = pdfBytes,
        }
    },
    Recipients = new[]
    {
        new SignatureRecipient
        {
            Name         = "Ada Lovelace",
            Email        = "ada@example.com",
            RoutingOrder = 1,
        }
    },
}, ct);

await sign.SendEnvelopeAsync(envelope.Id, ct);
```

After `SendEnvelopeAsync`, Adobe Sign emails the recipient. Status
updates flow through Adobe Sign Webhooks — wire those via
`FireflyFramework.Webhooks` to drive your own state machine.

### Refreshing the OAuth grant

`RefreshToken` is long-lived (Adobe issues 60-day defaults) but not
infinite. Track its issued-at and re-consent before expiry. The
adapter doesn't auto-refresh the *refresh token* — only the access
token.

### Voiding an in-flight agreement

```csharp
await sign.VoidEnvelopeAsync(envelopeId, "Customer requested cancellation", ct);
```

Adobe Sign records the cancellation reason and notifies all
participants by email. The agreement remains visible in the Adobe
Sign console with status `Cancelled`.

## Pitfalls and gotchas

- **Wrong region URL = silent 404s.** Adobe Sign's API surface is
  partitioned by data residency region. Set `BaseUrl` to the right
  pod (`eu1`, `na1`, `jp1`, etc.). The error response is a 404 with
  no body — easy to mis-diagnose.
- **`RefreshToken` rotation.** Adobe Sign refresh tokens expire if
  unused for 60 days. Build a watchdog that exchanges the token at
  least monthly to keep it fresh.
- **Consent flow needs human intervention.** The initial OAuth
  consent (which produces the refresh token) is interactive — there's
  no headless equivalent. Treat the refresh token as a
  configuration secret you provision once.
- **HTTP/2 + chunked uploads.** Adobe Sign's API rejects HTTP/2
  uploads with chunked encoding for some endpoints. The default
  HttpClient handles this correctly, but if you swap in a custom
  handler, test the upload path.
- **Agreement size limits.** Adobe Sign's per-document limit is 10
  MB. Larger documents must be split or compressed before upload.
- **Two roles for recipients.** Adobe Sign distinguishes `SIGNER`
  from `APPROVER`. The framework's `SignatureRecipient` only models
  signer; for approver workflows extend the adapter to set the
  vendor-specific role.

## Internals (for the curious)

- The adapter caches the access token in memory keyed on
  `ClientId`. Expiry is parsed from the OAuth response and a
  30-second skew buffer triggers re-acquisition before the natural
  expiry.
- Adobe Sign's REST v6 returns agreement ids as opaque base32
  strings. The adapter exposes them via `SignatureEnvelope.Id`
  without parsing.
- The HTTP layer uses `System.Net.Http.Json` for both directions —
  request and response bodies are serialised via the framework's
  default `JsonSerializerOptions`.

## Dependencies

| Reference                | Used for                              |
|--------------------------|---------------------------------------|
| `FireflyFramework.Ecm`   | `ISignatureEnvelopePort`, `[EcmAdapter]` |

`System.Net.Http.Json` (used for REST calls) ships in the .NET
framework — no package import needed.

## Java mapping

| .NET                                  | Java                                  |
|---------------------------------------|---------------------------------------|
| `AdobeSignSignatureEnvelopeAdapter`   | `AdobeSignSignatureEnvelopeAdapter`   |
| `AdobeSignOptions`                    | `AdobeSignProperties`                 |
