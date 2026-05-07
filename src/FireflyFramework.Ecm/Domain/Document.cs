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
