# FireflyFramework.Ecm.ESignature.DocuSign

## Overview

`FireflyFramework.Ecm.ESignature.DocuSign` is the **DocuSign
implementation of `ISignatureEnvelopePort`**. It authenticates with
JWT-grant (RSA-SHA256) and drives the DocuSign eSignature REST v2.1
envelope lifecycle: create, send, void, cancel, list-by-status.

Mirrors `org.fireflyframework:firefly-ecm-esignature-docusign`. The
endpoint paths, payload shapes, and JWT-grant flow are identical to
the Java line — a hybrid Java/.NET deployment can share the same
DocuSign integration key.

## Why a separate module?

DocuSign integrations carry meaningful operational baggage:

- **Vendor lock-in.** DocuSign's REST shape is unique; an abstraction
  hides that, but the SDK package itself is non-trivial.
- **Auth complexity.** JWT-grant requires an RSA private key and an
  application user. Other vendors use OAuth2 refresh tokens or
  client-credentials. Don't share auth code paths.
- **Compliance.** DocuSign produces legally binding envelopes; the
  integration must be auditable. Keeping the adapter in its own
  assembly makes the dependency explicit during security review.

## Adapter type

```csharp
[EcmAdapter("docusign",
    Description       = "DocuSign Envelope Adapter",
    SupportedFeatures = AdapterFeature.ESignatureEnvelopes
                      | AdapterFeature.ESignatureRequests,
    RequiredProperties = new[] { "AccountId", "IntegrationKey", "UserId", "RsaPrivateKey" })]
public sealed class DocuSignSignatureEnvelopeAdapter : ISignatureEnvelopePort { ... }
```

## Mental model

```
   application code
        │
        │  ISignatureEnvelopePort.CreateEnvelopeAsync(...)
        ▼
   ┌──────────────────────────┐
   │ DocuSignSignatureEnv.    │
   │ Adapter                  │
   └──────────┬───────────────┘
              │
              │ JWT-grant exchange
              ▼
   ┌──────────────────────────┐
   │ DocuSign OAuth           │
   │ POST /oauth/token        │  ← RSA-signed JWT assertion
   └──────────┬───────────────┘
              │ access token
              ▼
   ┌──────────────────────────┐
   │ DocuSign REST v2.1       │
   │ /accounts/{id}/envelopes │
   └──────────────────────────┘
```

The JWT-grant flow runs once per token lifetime (typically 1 hour),
then the cached access token is reused for envelope operations until
expiry.

## Configuration

```json
{
  "Firefly": {
    "Ecm": {
      "ESignature": {
        "DocuSign": {
          "BaseUrl":        "https://demo.docusign.net/restapi",
          "OAuthBaseUrl":   "https://account-d.docusign.com/oauth",
          "AccountId":      "<account guid>",
          "IntegrationKey": "<integration key>",
          "UserId":         "<api user guid>",
          "RsaPrivateKey":  "/secrets/docusign.pem"
        }
      }
    }
  }
}
```

| Property         | Demo value                                    | Production              |
|------------------|-----------------------------------------------|-------------------------|
| `BaseUrl`        | `https://demo.docusign.net/restapi`           | `https://www.docusign.net/restapi` (or your account's eu/au pod) |
| `OAuthBaseUrl`   | `https://account-d.docusign.com/oauth`        | `https://account.docusign.com/oauth`             |
| `AccountId`      | (from DocuSign dashboard)                     | (from DocuSign dashboard)                         |
| `IntegrationKey` | (from app registration)                       | (from app registration)                           |
| `UserId`         | (the application user the integration impersonates) |                                            |
| `RsaPrivateKey`  | path to PEM file or inline PEM string         | typically a secrets-manager reference             |

`RsaPrivateKey` may be a PEM file path or the inline PEM body. The
adapter detects which by looking for a PEM header (`-----BEGIN`).

## Wiring

```csharp
builder.Services.Configure<DocuSignOptions>(builder.Configuration.GetSection(DocuSignOptions.SectionName));
builder.Services.AddSingleton<DocuSignSignatureEnvelopeAdapter>();
```

For multi-account deployments (different DocuSign accounts per
tenant), wire one adapter per `DocuSignOptions` instance and route
through `AdapterRegistry.ResolveByType<...>("docusign-tenant-a")`.

## Common patterns

### Creating and sending an envelope

```csharp
var envelope = await sign.CreateEnvelopeAsync(new SignatureEnvelope
{
    Subject       = "Please sign your contract",
    Message       = "Hi, please review and sign by Friday.",
    Documents     = new[]
    {
        new SignatureDocument
        {
            Name        = "contract.pdf",
            ContentType = "application/pdf",
            Content     = pdfBytes,
        }
    },
    Recipients    = new[]
    {
        new SignatureRecipient
        {
            Name      = "Ada Lovelace",
            Email     = "ada@example.com",
            RoutingOrder = 1,
        }
    },
}, ct);

await sign.SendEnvelopeAsync(envelope.Id, ct);
```

After `SendEnvelopeAsync`, DocuSign emails the recipient. Their
clicks and signatures are mirrored to your endpoint via DocuSign
Connect (webhook) — wire that with
`FireflyFramework.Webhooks` (one of the bundled inbound-webhook
verifiers).

### Cancelling a stale envelope

```csharp
public async Task ExpireStaleEnvelopesAsync(CancellationToken ct)
{
    var stale = await sign.ListByStatusAsync("sent", olderThan: TimeSpan.FromDays(14), ct);
    foreach (var envelope in stale)
    {
        await sign.VoidEnvelopeAsync(envelope.Id, "Expired without signature", ct);
    }
}
```

`VoidEnvelopeAsync` (terminal state) and `CancelEnvelopeAsync`
(in-flight cancel) both translate to DocuSign's `voided` status —
the distinction is whether the envelope had progressed past `sent`.

### Routing recipients in series

```csharp
new[]
{
    new SignatureRecipient { Name = "Ada",  Email = "ada@…",  RoutingOrder = 1 },
    new SignatureRecipient { Name = "Bobby", Email = "bobby@…", RoutingOrder = 2 },
    new SignatureRecipient { Name = "Cleo", Email = "cleo@…", RoutingOrder = 3 },
}
```

DocuSign processes in `RoutingOrder` order. Multiple recipients
sharing a routing order sign in parallel.

## Pitfalls and gotchas

- **Demo vs. production URLs are different hosts.** A demo
  integration that points at production URLs will fail with a 404 on
  the OAuth endpoint. Always set both `BaseUrl` and `OAuthBaseUrl`
  consistently.
- **JWT-grant requires user consent.** The first time you authorise
  the integration, the application user must visit the consent URL
  (`{OAuthBaseUrl}/auth?response_type=code&...`) and approve. After
  that, the JWT-grant flow works headlessly.
- **`UserId` is not the same as the user's email.** It's the GUID
  shown on the DocuSign user's "API User Info" page.
- **`RsaPrivateKey` rotation.** When you rotate the key in DocuSign,
  the new public key takes ~10 minutes to propagate to their auth
  servers. Plan rotations during a maintenance window.
- **Document size limits apply.** DocuSign's own per-envelope
  document limit (typically 25 MB per document, 100 MB per envelope)
  is enforced server-side; the adapter doesn't pre-check.
- **Webhook signatures are separate.** The DocuSign Connect webhook
  uses HMAC-SHA-256 with a *different* secret from the JWT-grant key
  pair. Wire that via `FireflyFramework.Webhooks`'s generic HMAC
  verifier.

## Internals (for the curious)

- The adapter caches the access token in memory keyed by
  `IntegrationKey`. Token expiry is read from the JWT response; a
  60-second skew buffer triggers re-authentication before the actual
  expiry to avoid mid-call failures.
- The JWT assertion is signed with `RSA-SHA256` per DocuSign's
  spec. The adapter parses both PKCS#1 and PKCS#8 PEM formats.
- DocuSign's REST v2.1 returns envelope ids as opaque strings — the
  adapter exposes them as `SignatureEnvelope.Id` without parsing.

## Dependencies

| Reference                | Used for                              |
|--------------------------|---------------------------------------|
| `FireflyFramework.Ecm`   | `ISignatureEnvelopePort`, `[EcmAdapter]` |
| `DocuSign.eSign.dll`     | DocuSign SDK                          |

## Java mapping

| .NET                                | Java                              |
|-------------------------------------|-----------------------------------|
| `DocuSignSignatureEnvelopeAdapter`  | `DocuSignSignatureEnvelopeAdapter` |
| `DocuSignOptions`                   | `DocuSignProperties`              |
