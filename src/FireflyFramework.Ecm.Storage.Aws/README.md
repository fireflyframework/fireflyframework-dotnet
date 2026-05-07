# FireflyFramework.Ecm.Storage.Aws

Amazon S3 implementation of `IDocumentContentPort`. Stores document
binaries in a configurable bucket with optional path prefix, supports
streaming reads and HTTP byte-range requests.

Mirrors `org.fireflyframework:firefly-ecm-storage-aws`.

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

## Wiring

```csharp
using Amazon.S3;
using FireflyFramework.Ecm.Storage.Aws;

builder.Services.Configure<S3StorageOptions>(builder.Configuration.GetSection(S3StorageOptions.SectionName));
builder.Services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(/* credentials, region */));
builder.Services.AddSingleton<S3DocumentContentAdapter>();
```

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
