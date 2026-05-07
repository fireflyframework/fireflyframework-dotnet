# FireflyFramework.Ecm.Storage.Azure

Azure Blob Storage implementation of `IDocumentContentPort`. Supports
both connection-string and managed-identity (`DefaultAzureCredential`)
authentication, streaming reads, and HTTP byte-range requests.

Mirrors `org.fireflyframework:firefly-ecm-storage-azure`.

## Adapter type

```csharp
[EcmAdapter("azure-blob-content",
    Description       = "Azure Blob Storage Document Content Adapter",
    SupportedFeatures = AdapterFeature.ContentStorage
                      | AdapterFeature.Streaming
                      | AdapterFeature.CloudStorage,
    RequiredProperties = new[] { "ContainerName" },
    OptionalProperties = new[] { "ConnectionString", "AccountUrl" })]
public sealed class AzureBlobDocumentContentAdapter : IDocumentContentPort { ... }
```

## Configuration

Either `ConnectionString` (connection-string auth) or `AccountUrl`
(managed-identity auth via `DefaultAzureCredential`) is required.

```json
{
  "Firefly": {
    "Ecm": {
      "Storage": {
        "AzureBlob": {
          "ContainerName":    "documents",
          "ConnectionString": "<optional>",
          "AccountUrl":       "https://mystorageaccount.blob.core.windows.net"
        }
      }
    }
  }
}
```

## Wiring

```csharp
builder.Services.Configure<AzureBlobOptions>(builder.Configuration.GetSection(AzureBlobOptions.SectionName));
builder.Services.AddSingleton<AzureBlobDocumentContentAdapter>();
```

## Dependencies

| Reference                | Used for                              |
|--------------------------|---------------------------------------|
| `FireflyFramework.Ecm`   | `IDocumentContentPort`, `[EcmAdapter]` |
| `Azure.Storage.Blobs`    | Blob SDK                              |
| `Azure.Identity`         | `DefaultAzureCredential`              |

## Java mapping

| .NET                                  | Java                                      |
|---------------------------------------|-------------------------------------------|
| `AzureBlobDocumentContentAdapter`     | `AzureBlobDocumentContentAdapter`         |
| `AzureBlobOptions`                    | `AzureBlobProperties`                     |
