# FireflyFramework.Ecm.Storage.Azure

## Overview

`FireflyFramework.Ecm.Storage.Azure` is the **Azure Blob Storage
implementation of `IDocumentContentPort`**. It supports both
connection-string and managed-identity (`DefaultAzureCredential`)
authentication, streaming reads, and HTTP byte-range requests.

Mirrors `org.fireflyframework:firefly-ecm-storage-azure`. The blob
naming scheme, content-type pass-through, and metadata defaults are
identical to the Java line so a hybrid deployment can serve content
from either runtime against the same container.

## Why a separate module?

Azure SDK packages are large and distinct from AWS SDK; bundling
them with the framework would force every consumer to take both. The
storage adapters are split per-cloud so a service that uses Azure
Blob alone references only `Ecm.Storage.Azure`, and a service
deploying to multiple clouds references both adapters and lets the
`AdapterRegistry` arbitrate.

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

## Mental model

```
       application code
              │
              │ IDocumentContentPort.StoreContentAsync(documentId, stream, contentType, ct)
              ▼
     ┌──────────────────────────┐
     │ AzureBlobDocumentContent │
     │ Adapter                  │
     └──────────┬───────────────┘
                │ container.GetBlobClient(documentId)
                ▼
     ┌──────────────────────────┐
     │  BlobContainerClient     │   ← Azure SDK
     │  UploadAsync(stream)     │
     └──────────┬───────────────┘
                │
                ▼
     ┌──────────────────────────┐
     │ Storage account /        │
     │ <container>/<documentId> │
     └──────────────────────────┘
```

The adapter stores the document content directly under the container
root; it does *not* prefix paths. Multi-tenant deployments either use
one container per tenant (recommended) or wrap the adapter with a
prefixing decorator.

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

| Property            | Required (one of) | Notes                                                              |
|---------------------|-------------------|--------------------------------------------------------------------|
| `ContainerName`     | yes               | The container must exist                                           |
| `ConnectionString`  | one of            | Account-key or SAS connection string                               |
| `AccountUrl`        | one of            | When set without ConnectionString, uses `DefaultAzureCredential`   |

`DefaultAzureCredential` walks the standard credential chain
(environment, managed identity, Visual Studio, Azure CLI). For AKS,
this means workload identity by default — no secrets in
configuration.

## Wiring

```csharp
builder.Services.Configure<AzureBlobOptions>(builder.Configuration.GetSection(AzureBlobOptions.SectionName));
builder.Services.AddSingleton<AzureBlobDocumentContentAdapter>();
```

The adapter constructs its own `BlobContainerClient` from the bound
options. If you want to inject your own client (e.g. for custom
retry policies), expose a constructor overload — the default
construction is convenient but not the only path.

## Common patterns

### Workload identity in AKS

```yaml
# pod spec
serviceAccountName: orders-service
labels:
  azure.workload.identity/use: "true"
```

```json
{
  "Firefly": {
    "Ecm": {
      "Storage": {
        "AzureBlob": {
          "ContainerName": "documents",
          "AccountUrl":    "https://orders.blob.core.windows.net"
        }
      }
    }
  }
}
```

`DefaultAzureCredential` picks up the workload identity token from
the projected service account; the adapter calls Blob Storage with
the federated identity. No secrets in the cluster.

### Streaming a large upload

```csharp
[HttpPut("/documents/{id}/content")]
public async Task<IActionResult> Upload(string id, CancellationToken ct)
{
    await content.StoreContentAsync(id, Request.Body, Request.ContentType ?? "application/octet-stream", ct);
    return NoContent();
}
```

The adapter forwards `Request.Body` directly to
`BlobClient.UploadAsync` — the request body never lands fully in
memory. For uploads larger than ~256 MB, the SDK switches to block
uploads automatically.

### Soft-delete safety

Enable container-level soft-delete in Azure to allow recovery from
accidental delete. The adapter's `DeleteContentAsync` issues a hard
delete, but soft-delete settings on the container intercept it:

```bash
az storage account blob-service-properties update \
    --resource-group rg-orders \
    --account-name orders \
    --enable-delete-retention true \
    --delete-retention-days 14
```

## Pitfalls and gotchas

- **Container must exist.** The adapter doesn't auto-create. Set up
  containers in your deployment automation.
- **`AccountUrl` must include the scheme.** `mystorageaccount.blob.core.windows.net`
  without `https://` is rejected.
- **`ConnectionString` wins over `AccountUrl`.** If both are set,
  the adapter uses the connection string. This is by design — local
  development uses a connection string for the Azurite emulator, and
  production uses managed identity. Don't expect `AccountUrl` to
  override.
- **HNS-enabled accounts are flat-namespace by default.** Azure
  Data Lake Gen2 (HNS) is supported for read/write but the path
  semantics differ. Test on the storage tier you'll deploy to.
- **Snapshot retention is set on the container.** The adapter's
  `DeleteContentAsync` permanently deletes the current blob; if you
  rely on snapshots, use the SDK's snapshot API directly.
- **Large objects (>5 GB) need explicit transactional consistency.**
  Block uploads can fail mid-transfer; the SDK retries blocks
  individually but a stuck client may leave a partial upload. Run a
  cleanup job for blobs with uncommitted blocks.

## Internals (for the curious)

- The adapter creates a single `BlobContainerClient` per process
  and reuses it. The Azure SDK manages the underlying
  `HttpClient` pool and SAS token refresh internally.
- Byte-range reads pass the requested range to
  `BlobClient.DownloadStreamingAsync(new BlobDownloadOptions { Range = ... })`.
- Authentication priority is: `ConnectionString` first, then
  `AccountUrl + DefaultAzureCredential`. The adapter throws on
  startup if neither is set.

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
