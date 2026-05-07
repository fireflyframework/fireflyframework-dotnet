# FireflyFramework.Ecm.ESignature.Logalty

Logalty implementation of `ISignatureEnvelopePort`. Targets the
Logalty `/v1/processes` API for EU-qualified electronic signatures with
OAuth2 client-credentials authentication.

Mirrors `org.fireflyframework:firefly-ecm-esignature-logalty`.

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

## Wiring

```csharp
builder.Services.Configure<LogaltyOptions>(builder.Configuration.GetSection(LogaltyOptions.SectionName));
builder.Services.AddHttpClient<LogaltySignatureEnvelopeAdapter>();
```

## Lifecycle notes

- Logalty processes are immutable once created — `UpdateEnvelopeAsync`
  is a no-op that returns the supplied envelope unchanged.
- `SendEnvelopeAsync` calls `POST /v1/processes/{id}/start`.
- `VoidEnvelopeAsync` and `CancelEnvelopeAsync` both call
  `POST /v1/processes/{id}/cancel` with the supplied reason.

## Dependencies

| Reference                | Used for                                 |
|--------------------------|------------------------------------------|
| `FireflyFramework.Ecm`   | `ISignatureEnvelopePort`, `[EcmAdapter]` |
| `System.Net.Http.Json`   | REST calls                               |

## Java mapping

| .NET                                | Java                                  |
|-------------------------------------|---------------------------------------|
| `LogaltySignatureEnvelopeAdapter`   | `LogaltySignatureEnvelopeAdapter`     |
| `LogaltyOptions`                    | `LogaltyProperties`                   |
