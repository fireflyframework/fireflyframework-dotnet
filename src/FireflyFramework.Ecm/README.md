# FireflyFramework.Ecm

## Overview

`FireflyFramework.Ecm` is the **hexagonal abstraction tier** for
Enterprise Content Management. It defines fourteen ports that cover
the full ECM surface — documents, folders, versioning, search,
permissions, e-signature, audit, and intelligent document processing
— plus the discovery infrastructure (`[EcmAdapter]` attribute,
`AdapterFeature` flags, `AdapterRegistry`, `AdapterSelector`) that
lets a service register one or many implementations and pick the
right one at runtime by feature flag or priority.

It mirrors `org.fireflyframework:firefly-ecm` from the Java line.
The port names, feature flags, and adapter metadata are translated
one-to-one; provider adapters live in sibling assemblies (S3,
Azure Blob, DocuSign, AdobeSign, Logalty) to keep this module
dependency-free.

## Why a separate module?

ECM systems have a fragmented vendor landscape. A single deployment
may store documents on S3, sign them via DocuSign, validate
signatures via Logalty, and search them via OpenSearch. Each vendor
has its own SDK, auth model, and idiosyncrasies. Without a port
abstraction, application code couples to one stack and you can't swap
vendors without a rewrite.

The hexagonal layout in this module:

- Defines a *port* per capability — small, focused interfaces.
- Lets each provider implement only the ports it natively supports.
- Hands the application a `AdapterRegistry` + `AdapterSelector` that
  resolves the right implementation per call (by feature, by
  priority, by name).
- Keeps the port assembly dependency-free so consumers don't drag in
  AWS SDK / DocuSign SDK / Adobe SDK transitively.

## Mental model

```
                    Application service
                         │
                         ▼
              ┌──────────────────────┐
              │   AdapterSelector    │  picks by feature + priority
              │   <IDocumentContent> │
              └──────────┬───────────┘
                         │
                         ▼
              ┌──────────────────────┐
              │  AdapterRegistry     │  registered implementations
              └──────────┬───────────┘
                         │
       ┌─────────────────┼──────────────────┐
       │                 │                  │
       ▼                 ▼                  ▼
   ┌─────────┐     ┌─────────────┐    ┌──────────────────┐
   │ S3      │     │ Azure Blob  │    │ NoOpAdapter       │
   │ adapter │     │ adapter     │    │ (dry-run)         │
   └─────────┘     └─────────────┘    └──────────────────┘

       (each carries an [EcmAdapter] attribute declaring its
        SupportedFeatures, RequiredProperties, Priority)
```

The selector is a runtime decision-point — if you register both an
S3 adapter and an Azure Blob adapter, the selector picks the
highest-priority one supporting `AdapterFeature.ContentStorage`.
Toggle which one is "winning" by changing `Priority` or `Enabled` in
config.

## Ports

| Port                              | Purpose                                                              |
|-----------------------------------|----------------------------------------------------------------------|
| `IDocumentPort`                   | Document CRUD                                                        |
| `IDocumentContentPort`            | Binary get / store / delete / streaming / range                      |
| `IDocumentVersionPort`            | Version history, promote, rollback                                   |
| `IDocumentSearchPort`             | Search by query, folder, owner, status                               |
| `IFolderPort` / `IFolderHierarchyPort` | Folder lifecycle and traversal                                  |
| `IPermissionPort`                 | RBAC grants / revokes / checks                                       |
| `ISignatureEnvelopePort`          | Envelope creation, send, void, cancel                                |
| `ISignatureRequestPort`           | Per-signer requests and lifecycle markers                            |
| `ISignatureValidationPort`        | Validate signature / certificate / timestamp                         |
| `ISignatureProofPort`             | Per-envelope audit trail                                             |
| `IAuditPort`                      | Generic audit-event store                                            |
| `IDocumentClassificationPort`     | Detect document type with confidence score                           |
| `IDocumentExtractionPort`         | Pull structured data out of a document                               |
| `IDataExtractionPort`             | Targeted field extraction (form processing)                          |
| `IDocumentValidationPort`         | Structural / semantic validation                                     |
| `IDocumentSecurityPort`           | Antivirus scanning, encryption, decryption                           |

## Adapter framework

| Type                    | Purpose                                                              |
|-------------------------|----------------------------------------------------------------------|
| `[EcmAdapter]`          | Marks a class with `Type`, `Description`, `Priority`, `Enabled`, `SupportedFeatures`, `RequiredProperties`, `OptionalProperties` |
| `AdapterFeature`        | `[Flags]` enum — 38 capabilities (CRUD, ContentStorage, Versioning, Permissions, Search, ESignature variants, Encryption, IDP-related such as OCR / Classification / Extraction) |
| `AdapterIntrospection`  | Reads `[EcmAdapter]` metadata into `AdapterInfo`                     |
| `AdapterInfo`           | Static descriptor: `Type`, `Description`, `Priority`, `Enabled`, `SupportedFeatures`, `RequiredProperties`, `OptionalProperties`, `ImplementationType` |
| `AdapterRegistry`       | Runtime registry: `Register`, `GetInfo`, `All`, `SupportingFeature`, `Resolve<T>`, `ResolveByType<T>` |
| `AdapterSelector<TPort>` | Picks the highest-priority adapter implementing a port for a given feature |
| `AdapterValidationResult` | Result of a property / dependency check                            |
| `AdapterProfile`        | `NoOp`, `Local`, `Cloud`, `Enterprise`                                |

### `[EcmAdapter]` example

```csharp
[EcmAdapter("s3-content",
    Description        = "Amazon S3 Document Content Adapter",
    Priority           = 100,
    Enabled            = true,
    SupportedFeatures  = AdapterFeature.ContentStorage
                       | AdapterFeature.Streaming
                       | AdapterFeature.CloudStorage,
    RequiredProperties = new[] { "BucketName", "Region" },
    OptionalProperties = new[] { "AccessKey", "SecretKey", "Endpoint", "PathPrefix" })]
public sealed class S3DocumentContentAdapter : IDocumentContentPort { ... }
```

`AdapterIntrospection.GetInfo(typeof(...))` reads this attribute into
an `AdapterInfo` so the registry, selector, and admin endpoints can
operate on the metadata without instantiating the adapter.

## Built-in adapters

| Adapter                       | Implements                                                          |
|-------------------------------|---------------------------------------------------------------------|
| `NoOpAdapter`                 | All six core ports (Document / Content / Folder / Hierarchy / Permission / Audit) — drops everything; useful as a dry-run safety net |
| `LocalDocumentSearchAdapter`  | `IDocumentSearchPort` — in-memory; useful for single-node / tests   |
| `LocalPermissionAdapter`      | `IPermissionPort` — in-memory RBAC                                  |

## Provider adapters in sibling projects

| Project                                       | Implements                              |
|-----------------------------------------------|-----------------------------------------|
| `FireflyFramework.Ecm.Storage.Aws`            | `IDocumentContentPort` (S3)             |
| `FireflyFramework.Ecm.Storage.Azure`          | `IDocumentContentPort` (Azure Blob)     |
| `FireflyFramework.Ecm.ESignature.DocuSign`    | `ISignatureEnvelopePort`                |
| `FireflyFramework.Ecm.ESignature.AdobeSign`   | `ISignatureEnvelopePort`                |
| `FireflyFramework.Ecm.ESignature.Logalty`     | `ISignatureEnvelopePort`                |

## Domain types

| Type                          | Purpose                                                |
|-------------------------------|--------------------------------------------------------|
| `Document`                    | Metadata + lifecycle for a single content item         |
| `DocumentVersion`             | One version of a document                              |
| `Folder`                      | Folder metadata + parent reference                     |
| `AuditEvent`                  | Generic audit row                                      |
| `SignatureEnvelope`           | Envelope sent to one or more signers                   |
| `SignatureRequest`            | Per-signer request with status                         |
| `SignatureProof`              | Audit trail for a completed envelope                   |
| `DocumentProcessingRequest`   | Input to IDP pipeline                                  |
| `ClassificationResult`        | Detected document type + confidence                    |
| `ExtractedData`               | Structured form data                                   |
| `ValidationResult`            | Structural / semantic verdict                          |
| `DocumentProcessingResult`    | Aggregate IDP output                                   |

## Common patterns

### Registering and selecting an adapter

```csharp
using FireflyFramework.Ecm.Adapters;
using FireflyFramework.Ecm.Ports;

var registry = new AdapterRegistry();
registry.Register(new S3DocumentContentAdapter(s3, options));
registry.Register(new LocalDocumentSearchAdapter());

var selector = new AdapterSelector<IDocumentContentPort>(registry);
var content  = selector.PickByFeature(AdapterFeature.ContentStorage)!;
await content.StoreContentAsync(documentId, stream, "application/pdf", ct);
```

### Picking by feature flag

```csharp
// Default to S3, but for tenants flagged "qualified-signature-required"
// use the Logalty adapter that produces an EU-qualified e-signature.
var feature = tenant.RequiresQualifiedSignature
    ? AdapterFeature.ESignatureEnvelopes | AdapterFeature.SignatureValidation
    : AdapterFeature.ESignatureEnvelopes;

var sign = selector.PickByFeature(feature);
```

### Multiple environments via priority

```csharp
[EcmAdapter("s3-content",       Priority = 100, ...)]   // production
[EcmAdapter("local-disk-content", Priority = 10, ...)]  // dev fallback
```

The selector returns the higher-priority adapter when both are
registered; disable the higher-priority one (`Enabled = false`) to
fall through to the lower-priority one without rewiring DI.

### Dry-run with `NoOpAdapter`

When migrating a service to a new content store, register the
`NoOpAdapter` alongside the real one and route 1% of traffic to it
(by id-modulo or feature flag). Logs show what *would* have happened
without actually mutating anything.

## Pitfalls and gotchas

- **Ports are *small* on purpose.** A method that takes ten arguments
  should be split. The framework leans on
  `Document` / `SignatureEnvelope` etc. as carrier types so port
  surfaces stay readable.
- **`AdapterFeature` is a `[Flags]` enum.** Combine with `|`; check
  with `HasFlag(...)`. Don't try to compare with `==`.
- **`RequiredProperties` is informational.** The framework doesn't
  validate that the registered adapter's options carry those
  properties. Tools and admin UIs use the metadata to render setup
  forms — but startup validation is your job.
- **`AdapterRegistry` is *not* thread-safe for concurrent registration.**
  Register every adapter at startup, then treat the registry as
  read-only. Mutating it from multiple threads after the host is
  running is undefined behaviour.
- **`AdapterSelector` ignores disabled adapters.** Setting
  `Enabled = false` is the operator's "off switch" — the selector
  filters them out before priority sort.
- **Streaming reads must be disposed.** Adapters like S3 return
  `Stream` instances backed by HTTP responses. Wrap them in `using`
  or you'll leak sockets.

## Internals (for the curious)

- `AdapterIntrospection` uses reflection + caching: the first call
  for a given type pays for the metadata extraction, subsequent
  calls return the cached `AdapterInfo`. The cache is keyed on
  `Type` — different concrete classes share no state.
- `AdapterSelector<TPort>.PickByFeature` performs the predicate +
  sort each call. This is fine because adapter counts per port are
  typically <10. If your deployment grows beyond that, a per-feature
  pre-sorted index is a reasonable extension.
- The port interfaces are intentionally *not* async-disposable.
  Adapter authors that own connection-pooled state implement
  `IAsyncDisposable` themselves and are expected to be wired as
  singletons.

## Dependencies

| Reference                  | Used for                |
|----------------------------|-------------------------|
| `FireflyFramework.Kernel`  | Base exceptions         |

## Java mapping

| .NET                       | Java                                                  |
|----------------------------|-------------------------------------------------------|
| `[EcmAdapter]`             | `@EcmAdapter`                                         |
| `AdapterFeature`           | `AdapterFeature`                                      |
| `AdapterRegistry`          | `AdapterRegistry`                                     |
| `AdapterSelector<TPort>`   | `AdapterSelector`                                     |
| `IDocument*Port` / `IFolder*Port` / `ISignature*Port` | matching Java ports          |
| `NoOpAdapter`              | `NoOpGenericAdapter`                                  |
| `LocalDocumentSearchAdapter`/ `LocalPermissionAdapter` | `LocalDocumentSearchAdapter` / `LocalPermissionAdapter` |
