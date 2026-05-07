// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using FireflyFramework.Ecm.Domain;

namespace FireflyFramework.Ecm.Ports;

/// <summary>Document CRUD port. Mirrors Java <c>DocumentPort</c>.</summary>
public interface IDocumentPort
{
    Task<Document> CreateAsync(Document document, CancellationToken ct = default);
    Task<Document?> GetAsync(Guid documentId, CancellationToken ct = default);
    Task<Document> UpdateAsync(Document document, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid documentId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid documentId, CancellationToken ct = default);
}

/// <summary>Document binary content port. Mirrors Java <c>DocumentContentPort</c>.</summary>
public interface IDocumentContentPort
{
    Task<Stream> GetContentAsync(Guid documentId, CancellationToken ct = default);
    Task StoreContentAsync(Guid documentId, Stream content, string mimeType, CancellationToken ct = default);
    Task<bool> DeleteContentAsync(Guid documentId, CancellationToken ct = default);
    IAsyncEnumerable<byte[]> StreamAsync(Guid documentId, CancellationToken ct = default);
    Task<Stream> GetRangeContentAsync(Guid documentId, long start, long end, CancellationToken ct = default);
}

/// <summary>Document version port.</summary>
public interface IDocumentVersionPort
{
    Task<DocumentVersion> CreateVersionAsync(Guid documentId, string? changeDescription, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentVersion>> GetVersionsAsync(Guid documentId, CancellationToken ct = default);
    Task PromoteVersionAsync(Guid versionId, CancellationToken ct = default);
    Task RollbackAsync(Guid versionId, CancellationToken ct = default);
}

/// <summary>Document search port.</summary>
public interface IDocumentSearchPort
{
    Task<IReadOnlyList<Document>> SearchAsync(string query, CancellationToken ct = default);
    Task<IReadOnlyList<Document>> FindByFolderAsync(Guid folderId, CancellationToken ct = default);
    Task<IReadOnlyList<Document>> FindByOwnerAsync(string owner, CancellationToken ct = default);
    Task<IReadOnlyList<Document>> FindByStatusAsync(DocumentStatus status, CancellationToken ct = default);
}

/// <summary>Folder lifecycle port.</summary>
public interface IFolderPort
{
    Task<Folder> CreateAsync(Folder folder, CancellationToken ct = default);
    Task<Folder?> GetAsync(Guid folderId, CancellationToken ct = default);
    Task<Folder> UpdateAsync(Folder folder, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid folderId, CancellationToken ct = default);
    Task<Folder> MoveAsync(Guid folderId, Guid? newParentId, CancellationToken ct = default);
}

/// <summary>Folder hierarchy traversal port.</summary>
public interface IFolderHierarchyPort
{
    Task<IReadOnlyList<Folder>> ListChildrenAsync(Guid folderId, CancellationToken ct = default);
    Task<Folder?> GetParentAsync(Guid folderId, CancellationToken ct = default);
    Task<string> GetPathAsync(Guid folderId, CancellationToken ct = default);
}

/// <summary>Permission / RBAC port.</summary>
public interface IPermissionPort
{
    Task GrantAsync(string principal, string resource, string action, CancellationToken ct = default);
    Task RevokeAsync(string principal, string resource, string action, CancellationToken ct = default);
    Task<bool> CheckAsync(string principal, string resource, string action, CancellationToken ct = default);
}

/// <summary>Signature envelope port. Mirrors Java <c>SignatureEnvelopePort</c>.</summary>
public interface ISignatureEnvelopePort
{
    Task<SignatureEnvelope> CreateEnvelopeAsync(SignatureEnvelope envelope, CancellationToken ct = default);
    Task<SignatureEnvelope?> GetEnvelopeAsync(Guid envelopeId, CancellationToken ct = default);
    Task<SignatureEnvelope> UpdateEnvelopeAsync(SignatureEnvelope envelope, CancellationToken ct = default);
    Task SendEnvelopeAsync(Guid envelopeId, string? sentBy, CancellationToken ct = default);
    Task VoidEnvelopeAsync(Guid envelopeId, string reason, string? voidedBy, CancellationToken ct = default);
    Task CancelEnvelopeAsync(Guid envelopeId, CancellationToken ct = default);
    Task<IReadOnlyList<SignatureEnvelope>> GetEnvelopesByStatusAsync(SignatureEnvelopeStatus status, CancellationToken ct = default);
}

/// <summary>Signature request port.</summary>
public interface ISignatureRequestPort
{
    Task<SignatureRequest> CreateAsync(SignatureRequest request, CancellationToken ct = default);
    Task<SignatureRequest?> GetAsync(Guid requestId, CancellationToken ct = default);
    Task<IReadOnlyList<SignatureRequest>> GetByEnvelopeAsync(Guid envelopeId, CancellationToken ct = default);
    Task<IReadOnlyList<SignatureRequest>> GetBySignerAsync(Guid signerId, CancellationToken ct = default);
    Task MarkAsViewedAsync(Guid requestId, CancellationToken ct = default);
    Task MarkAsSignedAsync(Guid requestId, CancellationToken ct = default);
    Task MarkAsDeclinedAsync(Guid requestId, string reason, CancellationToken ct = default);
}

public interface ISignatureValidationPort
{
    Task<bool> ValidateSignatureAsync(byte[] signedDocument, byte[] signature, CancellationToken ct = default);
    Task<bool> ValidateCertificateAsync(byte[] certificate, CancellationToken ct = default);
    Task<bool> ValidateTimestampAsync(byte[] timestamp, CancellationToken ct = default);
}

public sealed record SignatureProof(Guid EnvelopeId, IReadOnlyList<AuditEvent> AuditTrail, byte[] Certificate);

public interface ISignatureProofPort
{
    Task<SignatureProof> GetSignatureProofAsync(Guid envelopeId, CancellationToken ct = default);
    Task<IReadOnlyList<AuditEvent>> GetAuditTrailAsync(Guid envelopeId, CancellationToken ct = default);
}

public interface IAuditPort
{
    Task<AuditEvent> CreateAuditEventAsync(AuditEvent @event, CancellationToken ct = default);
    Task<IReadOnlyList<AuditEvent>> ListAuditEventsAsync(string? resourceId = null, CancellationToken ct = default);
}
