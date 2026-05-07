namespace FireflyFramework.Ecm.Domain;

public enum DocumentStatus { Draft, Active, Archived, Deleted }

public sealed record Document(
    Guid Id,
    string Name,
    string? Owner,
    DocumentStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ModifiedAt,
    string? ContentHash,
    long? SizeBytes,
    string? MimeType,
    string? FolderId,
    Dictionary<string, object?>? Metadata);

public sealed record DocumentVersion(
    Guid Id,
    Guid DocumentId,
    int VersionNumber,
    DocumentVersionStatus Status,
    string? ChangeDescription,
    DateTimeOffset CreatedAt,
    string? CreatedBy);

public enum DocumentVersionStatus { Current, Superseded }

public sealed record Folder(
    Guid Id,
    string Name,
    Guid? ParentId,
    string Path,
    string? Owner,
    DateTimeOffset CreatedAt);

public sealed record AuditEvent(
    Guid Id,
    string Action,
    string? Principal,
    string ResourceType,
    string ResourceId,
    DateTimeOffset Timestamp,
    Dictionary<string, object?>? Details);

public enum SignatureEnvelopeStatus { Draft, Sent, Signed, Completed, Cancelled, Voided }

public sealed record SignatureEnvelope(
    Guid Id,
    string Name,
    IReadOnlyList<Guid> DocumentIds,
    IReadOnlyList<SignatureRequest> Signers,
    SignatureEnvelopeStatus Status,
    string Provider,
    DateTimeOffset CreatedAt);

public sealed record SignatureRequest(
    Guid Id,
    Guid EnvelopeId,
    Guid SignerId,
    string SignerEmail,
    string SignerName,
    SignatureRequestStatus Status,
    DateTimeOffset CreatedAt);

public enum SignatureRequestStatus { Pending, Viewed, Signed, Declined }
