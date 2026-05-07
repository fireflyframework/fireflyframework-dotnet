# FireflyFramework.Ecm.ESignature.AdobeSign

Adobe Sign implementation of `ISignatureEnvelopePort`. Calls the
Adobe Sign REST v6 agreements API with OAuth2 refresh-token
authentication.

Mirrors `org.fireflyframework:firefly-ecm-esignature-adobe-sign`.

## Adapter type

```csharp
[EcmAdapter("adobe-sign",
    Description       = "Adobe Sign Envelope Adapter",
    SupportedFeatures = AdapterFeature.ESignatureEnvelopes
                      | AdapterFeature.ESignatureRequests,
    RequiredProperties = new[] { "ClientId", "ClientSecret", "RefreshToken" })]
public sealed class AdobeSignSignatureEnvelopeAdapter : ISignatureEnvelopePort { ... }
```

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

The adapter exchanges the refresh token for an access token on demand
and caches it in memory until expiry.

## Wiring

```csharp
builder.Services.Configure<AdobeSignOptions>(builder.Configuration.GetSection(AdobeSignOptions.SectionName));
builder.Services.AddHttpClient<AdobeSignSignatureEnvelopeAdapter>();
```

## Dependencies

| Reference                | Used for                              |
|--------------------------|---------------------------------------|
| `FireflyFramework.Ecm`   | `ISignatureEnvelopePort`, `[EcmAdapter]` |
| `System.Net.Http.Json`   | REST calls                            |

## Java mapping

| .NET                                  | Java                                  |
|---------------------------------------|---------------------------------------|
| `AdobeSignSignatureEnvelopeAdapter`   | `AdobeSignSignatureEnvelopeAdapter`   |
| `AdobeSignOptions`                    | `AdobeSignProperties`                 |
