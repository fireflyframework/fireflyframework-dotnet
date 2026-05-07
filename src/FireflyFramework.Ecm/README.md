# FireflyFramework.Ecm

Hexagonal ports for Enterprise Content Management: documents, folders, versioning, search, permissions, e-signature envelopes/requests/validation/proof, audit. Mirrors `fireflyframework-ecm`.

## Ports

| Port | Purpose |
|---|---|
| `IDocumentPort` | Document CRUD |
| `IDocumentContentPort` | Binary get / store / delete / streaming / range |
| `IDocumentVersionPort` | Version history, promote, rollback |
| `IDocumentSearchPort` | Search by query, folder, owner, status |
| `IFolderPort` / `IFolderHierarchyPort` | Folder lifecycle and traversal |
| `IPermissionPort` | RBAC grants / revokes / checks |
| `ISignatureEnvelopePort` | Envelope creation, send, void, cancel |
| `ISignatureRequestPort` | Per-signer requests + lifecycle markers |
| `ISignatureValidationPort` | Validate signature / certificate / timestamp |
| `ISignatureProofPort` | Audit trail per envelope |
| `IAuditPort` | Generic audit log |

## Adapters in this repo

| Adapter | Implements | Notes |
|---|---|---|
| `FireflyFramework.Ecm.Storage.Aws` | `IDocumentContentPort` (`S3DocumentContentAdapter`) | AWSSDK.S3, multipart, range, streaming |
| `FireflyFramework.Ecm.Storage.Azure` | `IDocumentContentPort` (`AzureBlobDocumentContentAdapter`) | Azure.Storage.Blobs + DefaultAzureCredential |
| `FireflyFramework.Ecm.ESignature.DocuSign` | `ISignatureEnvelopePort` | JWT-grant auth, REST v2.1 envelope CRUD |
| `FireflyFramework.Ecm.ESignature.AdobeSign` | `ISignatureEnvelopePort` | OAuth2 refresh-token, REST v6 agreements |
| `FireflyFramework.Ecm.ESignature.Logalty` | `ISignatureEnvelopePort` | OAuth2 client-credentials, EU qualified signatures |

## `[EcmAdapter]` discovery

Tag any adapter class with `[EcmAdapter("type", SupportedFeatures = ..., RequiredProperties = ..., OptionalProperties = ...)]` so the registry can describe and select it at runtime. The base attribute carries the metadata; the wiring is deliberately left to consumer applications so they can choose how (DI conditionally, factory, etc.) to instantiate adapters.
