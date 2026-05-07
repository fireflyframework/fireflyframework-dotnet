# FireflyFramework.Ecm.ESignature.DocuSign

DocuSign implementation of `ISignatureEnvelopePort`. Authenticates with
JWT-grant (RSA-SHA256) and drives the DocuSign eSignature REST v2.1
envelope lifecycle: create, send, void, cancel, list-by-status.

Mirrors `org.fireflyframework:firefly-ecm-esignature-docusign`.

## Adapter type

```csharp
[EcmAdapter("docusign",
    Description       = "DocuSign Envelope Adapter",
    SupportedFeatures = AdapterFeature.ESignatureEnvelopes
                      | AdapterFeature.ESignatureRequests,
    RequiredProperties = new[] { "AccountId", "IntegrationKey", "UserId", "RsaPrivateKey" })]
public sealed class DocuSignSignatureEnvelopeAdapter : ISignatureEnvelopePort { ... }
```

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

`RsaPrivateKey` may be a PEM file path or the inline PEM body.

## Wiring

```csharp
builder.Services.Configure<DocuSignOptions>(builder.Configuration.GetSection(DocuSignOptions.SectionName));
builder.Services.AddSingleton<DocuSignSignatureEnvelopeAdapter>();
```

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
