# FireflyFramework.Ecm.ESignature.Logalty

## Overview

`FireflyFramework.Ecm.ESignature.Logalty` is the **Logalty
implementation of `ISignatureEnvelopePort`**, focused on EU-qualified
electronic signatures (eIDAS-compliant). It targets the Logalty
`/v1/processes` API with OAuth2 client-credentials authentication.

Mirrors `org.fireflyframework:firefly-ecm-esignature-logalty`. Logalty
is a Spanish/EU electronic-signature provider; this adapter is the
right choice when:

- The signature must be eIDAS-qualified (legally equivalent to a
  handwritten signature in EU jurisdictions).
- You need long-term archival of qualified signatures.
- Spain/EU residency rules apply.

For US-centric or general workflows, prefer DocuSign or Adobe Sign.

## Why a separate module?

EU-qualified e-signatures have stricter compliance requirements than
ordinary e-signatures (qualified-trust-service-provider validation,
long-term archival, certificate chains). Logalty's "process" model
also differs from envelope-style providers: a process is immutable
once started, and lifecycle operations are deliberately limited.

Keeping Logalty in its own assembly:

- Avoids confusion with envelope-style adapters where update is
  meaningful.
- Lets the consumer pull in only the qualified-signature SDK if
  they're in the EU compliance lane.

## Adapter type

```csharp
[EcmAdapter("logalty",
    Description       = "Logalty Qualified Signature Adapter (EU)",
    SupportedFeatures = AdapterFeature.ESignatureEnvelopes
                      | AdapterFeature.ESignatureRequests
                      | AdapterFeature.SignatureValidation,
    RequiredProperties = new[] { "ClientId", "ClientSecret" })]
public sealed class LogaltySignatureEnvelopeAdapter : ISignatureEnvelopePort { ... }
```

Note that the adapter advertises `AdapterFeature.SignatureValidation`
in addition to envelopes — Logalty includes long-term verifiability
guarantees that other adapters don't.

## Mental model

```
   application code
        │
        │  ISignatureEnvelopePort.CreateEnvelopeAsync(...)
        ▼
   ┌──────────────────────────┐
   │ LogaltySignatureEnv.     │
   │ Adapter                  │
   └──────────┬───────────────┘
              │
              │ client_credentials grant
              ▼
   ┌──────────────────────────┐
   │ Logalty OAuth2           │
   │ POST /oauth/token        │
   └──────────┬───────────────┘
              │ access token
              ▼
   ┌──────────────────────────┐
   │ Logalty API              │
   │ /v1/processes            │
   └──────────────────────────┘
```

## Configuration

```json
{
  "Firefly": {
    "Ecm": {
      "ESignature": {
        "Logalty": {
          "BaseUrl":      "https://api.logalty.com",
          "ClientId":     "<oauth2 client id>",
          "ClientSecret": "<oauth2 client secret>"
        }
      }
    }
  }
}
```

| Property        | Notes                                                                  |
|-----------------|------------------------------------------------------------------------|
| `BaseUrl`       | Production: `https://api.logalty.com`; sandbox: `https://api-sandbox.logalty.com` |
| `ClientId`      | OAuth2 client id from your Logalty integration                         |
| `ClientSecret`  | OAuth2 client secret                                                   |

## Wiring

```csharp
builder.Services.Configure<LogaltyOptions>(builder.Configuration.GetSection(LogaltyOptions.SectionName));
builder.Services.AddHttpClient<LogaltySignatureEnvelopeAdapter>();
```

## Lifecycle notes

Logalty processes are **immutable once created**. The adapter maps
the framework's port to Logalty's actual capabilities:

| Port method                       | Logalty endpoint                              | Notes                                      |
|-----------------------------------|-----------------------------------------------|--------------------------------------------|
| `CreateEnvelopeAsync`             | `POST /v1/processes`                          | Creates the process                        |
| `UpdateEnvelopeAsync`             | (no-op)                                       | Returns the supplied envelope unchanged    |
| `SendEnvelopeAsync`               | `POST /v1/processes/{id}/start`               | Activates and notifies signers             |
| `VoidEnvelopeAsync`               | `POST /v1/processes/{id}/cancel`              | Cancels with the supplied reason           |
| `CancelEnvelopeAsync`             | `POST /v1/processes/{id}/cancel`              | Identical behaviour to Void in Logalty     |
| `GetEnvelopeAsync`                | `GET /v1/processes/{id}`                      | Returns the current state                  |
| `ListByStatusAsync`               | `GET /v1/processes?status=...`                | Pagination through `next` link             |

## Common patterns

### Issuing a qualified signature

```csharp
var envelope = await sign.CreateEnvelopeAsync(new SignatureEnvelope
{
    Subject  = "Loan agreement",
    Message  = "Por favor firme el contrato adjunto",
    Documents = new[]
    {
        new SignatureDocument
        {
            Name        = "loan-agreement.pdf",
            ContentType = "application/pdf",
            Content     = pdfBytes,
        }
    },
    Recipients = new[]
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

The signer receives a Logalty link, completes the qualified-signature
ceremony (typically including SMS verification or eID), and the
process moves to `signed`.

### Cancelling with a documented reason

```csharp
await sign.CancelEnvelopeAsync(processId, "Customer rescinded request within cooling-off period", ct);
```

The cancellation reason is stored on the process and surfaces in the
qualified-signature audit trail. Always supply something meaningful.

## Pitfalls and gotchas

- **Processes are immutable.** Once created, you can't change the
  document set, recipient list, or message. If a recipient's email
  has a typo, you must cancel and create a new process.
- **`VoidEnvelopeAsync` and `CancelEnvelopeAsync` map to the same
  endpoint.** This is a Logalty quirk — they don't distinguish a
  voided-after-send from a cancelled-mid-flight. The framework
  preserves both methods for API compatibility, but their behaviour
  is identical.
- **Token expiry is short.** Logalty's client-credentials tokens
  expire in 1 hour. The adapter caches and refreshes automatically.
- **Sandbox base URL is different.** Don't run against production
  during integration testing — sandbox is `https://api-sandbox.logalty.com`.
  Sandbox processes don't produce legally-binding signatures.
- **Document size and format.** Logalty accepts PDF only and
  enforces a per-document size limit (~10 MB). Convert other formats
  client-side before upload.
- **Recipient verification.** Qualified signatures require strong
  identity verification of the signer (eID, SMS+TOTP, video). Make
  sure your recipient records carry the right contact channel for
  verification — the adapter doesn't enforce this.

## Internals (for the curious)

- The adapter caches the access token in memory keyed on
  `ClientId`. Expiry is read from the OAuth response.
- The `UpdateEnvelopeAsync` no-op intentionally returns the supplied
  envelope unchanged so call-site code that expects a return value
  doesn't break. This is consistent with the Java line.
- Listing operations use Logalty's `next` link for pagination — the
  adapter follows links opaquely without imposing offset semantics.

## Dependencies

| Reference                | Used for                                 |
|--------------------------|------------------------------------------|
| `FireflyFramework.Ecm`   | `ISignatureEnvelopePort`, `[EcmAdapter]` |

`System.Net.Http.Json` (used for REST calls) ships in the .NET
framework — no package import needed.

## Java mapping

| .NET                                | Java                                  |
|-------------------------------------|---------------------------------------|
| `LogaltySignatureEnvelopeAdapter`   | `LogaltySignatureEnvelopeAdapter`     |
| `LogaltyOptions`                    | `LogaltyProperties`                   |
