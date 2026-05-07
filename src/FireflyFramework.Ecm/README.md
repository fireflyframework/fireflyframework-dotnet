# FireflyFramework.Ecm

Hexagonal ECM (Enterprise Content Management) abstraction. Defines
fourteen ports covering documents, folders, versioning, search,
permissions, e-signature, audit, and intelligent document processing.
Adapter-discovery infrastructure lets a service register one or many
implementations and pick the right one at runtime by feature flag or
priority.

Mirrors `org.fireflyframework:firefly-ecm`.

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

`Document`, `DocumentVersion`, `Folder`, `AuditEvent`,
`SignatureEnvelope`, `SignatureRequest`, `SignatureProof`, plus the IDP
records `DocumentProcessingRequest`, `ClassificationResult`,
`ExtractedData`, `ValidationResult`, `DocumentProcessingResult`.

## Usage

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
