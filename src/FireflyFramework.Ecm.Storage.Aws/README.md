# FireflyFramework.Ecm.Storage.Aws

## Overview

`FireflyFramework.Ecm.Storage.Aws` is the **Amazon S3 implementation
of `IDocumentContentPort`**. It stores document binaries in a
configurable bucket with optional path prefix, supports streaming
reads, and honours HTTP byte-range requests for partial downloads
(e.g. resumable uploads).

Mirrors `org.fireflyframework:firefly-ecm-storage-aws`. The behaviour
matches the Java line: same key layout, same content-type pass-through,
same metadata defaults.

## Why a separate module?

Pulling AWS SDK into a service that doesn't use AWS storage would
add ~3 MB of indirect dependencies and dozens of transitive packages
the consumer doesn't need. Keeping the S3 adapter in its own assembly:

- Lets a service opt in to S3 by referencing `Ecm.Storage.Aws`
  *and* `Ecm`. Without this assembly, the AWS SDK isn't loaded at
  all.
- Lets multiple storage adapters (S3 + Azure Blob + on-prem) coexist
  in the same registry without conflicting on SDK versions.
- Mirrors the Java line's modular packaging convention so the
  hybrid Java/.NET deployment story remains consistent.

## Adapter type

```csharp
[EcmAdapter("s3-content",
    Description       = "Amazon S3 Document Content Adapter",
    SupportedFeatures = AdapterFeature.ContentStorage
                      | AdapterFeature.Streaming
                      | AdapterFeature.CloudStorage,
    RequiredProperties = new[] { "BucketName", "Region" },
    OptionalProperties = new[] { "AccessKey", "SecretKey", "Endpoint", "PathPrefix" })]
public sealed class S3DocumentContentAdapter : IDocumentContentPort { ... }
```

`AdapterSelector<IDocumentContentPort>` picks this adapter when
`AdapterFeature.ContentStorage` is requested and no higher-priority
adapter is registered.

## Mental model

```
       application code
              │
              │ IDocumentContentPort.StoreContentAsync(documentId, stream, contentType, ct)
              ▼
     ┌──────────────────────┐
     │ S3DocumentContent    │
     │ Adapter              │
     └──────────┬───────────┘
                │ key = $"{PathPrefix}{documentId}"
                ▼
     ┌──────────────────────┐
     │     IAmazonS3        │   ← AWS SDK
     │  PutObjectAsync      │
     └──────────┬───────────┘
                │
                ▼
     ┌──────────────────────┐
     │  S3 bucket           │
     │  bucket/[prefix]/id  │
     └──────────────────────┘
```

The adapter is intentionally thin — it doesn't transform content,
encrypt, or compress. Wrap it with a decorator if you need any of
those transforms; chaining adapters keeps each concern isolated.

## Configuration

```json
{
  "Firefly": {
    "Ecm": {
      "Storage": {
        "S3": {
          "BucketName": "documents",
          "Region":     "eu-west-1",
          "AccessKey":  "<optional, otherwise default credential chain>",
          "SecretKey":  "<optional>",
          "Endpoint":   "<optional, e.g. https://minio.local for self-hosted>",
          "PathPrefix": "tenant-a/"
        }
      }
    }
  }
}
```

| Property      | Required | Notes                                                                |
|---------------|----------|----------------------------------------------------------------------|
| `BucketName`  | yes      | The bucket must exist; the adapter does not auto-create it           |
| `Region`      | yes      | AWS region (e.g. `eu-west-1`)                                        |
| `AccessKey`   | no       | If both `AccessKey` and `SecretKey` are present, used directly        |
| `SecretKey`   | no       | Otherwise the AWS default credential chain is used (env vars, IAM role) |
| `Endpoint`    | no       | Override for S3-compatible services (MinIO, Wasabi, Backblaze B2)    |
| `PathPrefix`  | no       | Prepended to every key — useful for multi-tenant isolation           |

For production AWS, prefer IAM-role credentials (omit `AccessKey` /
`SecretKey`) and let the EC2/EKS/ECS metadata service supply
short-lived credentials.

## Wiring

```csharp
using Amazon.S3;
using FireflyFramework.Ecm.Storage.Aws;

builder.Services.Configure<S3StorageOptions>(builder.Configuration.GetSection(S3StorageOptions.SectionName));
builder.Services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(/* credentials, region */));
builder.Services.AddSingleton<S3DocumentContentAdapter>();

// Register with the EcmAdapterRegistry so the selector can find it
builder.Services.AddSingleton<IHostedService, EcmAdapterBootstrapper>();
```

The `IAmazonS3` factory is intentionally separate from the adapter
registration. This lets you supply a custom `AWSCredentials`
implementation (e.g. one that refreshes from Vault) without forking
the adapter.

## Common patterns

### MinIO for local development

```json
{
  "Firefly": {
    "Ecm": {
      "Storage": {
        "S3": {
          "BucketName": "documents",
          "Region":     "us-east-1",
          "Endpoint":   "http://localhost:9000",
          "AccessKey":  "minioadmin",
          "SecretKey":  "minioadmin"
        }
      }
    }
  }
}
```

Set `ForcePathStyle = true` on the `AmazonS3Client` config when
using MinIO — virtual-host style URLs assume DNS that MinIO doesn't
provide.

### Multi-tenant prefix

```csharp
public sealed class TenantAwareContentAdapter(
    S3DocumentContentAdapter inner,
    ITenantContext tenant) : IDocumentContentPort
{
    public Task<Stream?> GetContentAsync(string id, CancellationToken ct) =>
        inner.GetContentAsync($"{tenant.Id}/{id}", ct);

    public Task StoreContentAsync(string id, Stream content, string ct, CancellationToken token) =>
        inner.StoreContentAsync($"{tenant.Id}/{id}", content, ct, token);

    // ... etc
}
```

Or — simpler — set `PathPrefix` per tenant via configuration if
each tenant gets its own host instance.

### Streaming a large download

```csharp
[HttpGet("/documents/{id}/content")]
public async Task<IActionResult> Download(string id)
{
    var stream = await content.GetContentAsync(id, ct);
    if (stream is null) return NotFound();
    return File(stream, "application/octet-stream", $"{id}.bin", enableRangeProcessing: true);
}
```

`enableRangeProcessing: true` makes ASP.NET respect the byte-range
the client sent. The adapter passes the range through to S3, so the
network round-trip is bounded by the requested slice.

## Pitfalls and gotchas

- **The bucket must exist.** The adapter doesn't `CreateBucketAsync`.
  Pre-create the bucket via Terraform / CloudFormation / `aws cli`.
- **`PathPrefix` does not delimit on `/`.** A prefix like
  `tenant-a` (no trailing slash) means objects at `tenant-a-other/...`
  also match prefix listings. Always end the prefix with `/` if you
  rely on it for isolation.
- **`Endpoint` overrides region URL inference.** If you set
  `Endpoint` but leave the standard `Region`, the SDK still signs
  with the configured region — make sure your S3-compatible target
  understands that.
- **Default credential chain caches.** When IAM role credentials
  rotate, the in-process credential cache picks up the new
  credentials within a few minutes. Don't try to short-circuit the
  cache; trust the SDK.
- **Object metadata is empty by default.** If you need
  `Content-Type` / `Cache-Control` / custom metadata on the stored
  object, extend the adapter — the port doesn't accept them.
- **Eventual consistency on listings.** S3 read-after-write is
  strong; listings are still strongly consistent for new objects but
  may briefly lag for deletions. If your code lists then reads,
  expect occasional gaps in the millisecond range.

## Internals (for the curious)

- `S3DocumentContentAdapter` uses `PutObjectAsync` for writes and
  `GetObjectAsync` for reads. The streaming response from
  `GetObjectAsync` is returned to the caller without buffering — the
  client transfers data on demand.
- Byte-range requests use `GetObjectRequest.ByteRange` directly; the
  adapter doesn't simulate ranges client-side.
- The adapter intentionally does *not* call `EnsureSuccessStatusCode`
  or wrap exceptions. AWS SDK exceptions surface unchanged so callers
  can switch on `AmazonS3Exception.ErrorCode` if they want to handle
  specific failure modes.

## Dependencies

| Reference                | Used for           |
|--------------------------|--------------------|
| `FireflyFramework.Ecm`   | `IDocumentContentPort`, `[EcmAdapter]` |
| `AWSSDK.S3`              | S3 client          |

## Java mapping

| .NET                            | Java                              |
|---------------------------------|-----------------------------------|
| `S3DocumentContentAdapter`      | `S3DocumentContentAdapter`        |
| `S3StorageOptions`              | `S3StorageProperties`             |
