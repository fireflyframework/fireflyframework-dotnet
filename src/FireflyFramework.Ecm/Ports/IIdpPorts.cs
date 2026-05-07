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

// ───── Intelligent Document Processing (IDP) ports ─────
// Mirror the Java org.fireflyframework.ecm.port.idp package.

public sealed record DocumentProcessingRequest(
    Guid DocumentId,
    string MimeType,
    Stream Content,
    IReadOnlyDictionary<string, string>? Hints = null);

public sealed record ClassificationResult(
    string DocumentType,
    double Confidence,
    IReadOnlyDictionary<string, double>? Alternatives = null);

public sealed record ExtractedData(
    IReadOnlyDictionary<string, string> Fields,
    IReadOnlyDictionary<string, double>? FieldConfidences = null);

public sealed record ValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyDictionary<string, string>? FieldStatuses = null)
{
    public static ValidationResult Valid() => new(true, Array.Empty<string>(), null);
    public static ValidationResult Invalid(params string[] errors) => new(false, errors, null);
}

public sealed record DocumentProcessingResult(
    Guid DocumentId,
    ClassificationResult? Classification,
    ExtractedData? ExtractedData,
    ValidationResult? Validation);

/// <summary>Detects what type a document is. Mirrors Java <c>DocumentClassificationPort</c>.</summary>
public interface IDocumentClassificationPort
{
    Task<ClassificationResult> ClassifyAsync(DocumentProcessingRequest request, CancellationToken ct = default);
}

/// <summary>Pulls structured data out of a document. Mirrors Java <c>DocumentExtractionPort</c>.</summary>
public interface IDocumentExtractionPort
{
    Task<ExtractedData> ExtractAsync(DocumentProcessingRequest request, CancellationToken ct = default);
}

/// <summary>Pulls structured fields out of a document for downstream business logic.</summary>
public interface IDataExtractionPort
{
    Task<ExtractedData> ExtractFieldsAsync(DocumentProcessingRequest request, IReadOnlyList<string> fieldNames, CancellationToken ct = default);
}

/// <summary>
/// Validates a document either structurally (e.g., schema) or semantically (e.g., the
/// extracted fields satisfy business rules). Mirrors Java <c>DocumentValidationPort</c>.
/// </summary>
public interface IDocumentValidationPort
{
    Task<ValidationResult> ValidateAsync(DocumentProcessingRequest request, CancellationToken ct = default);
    Task<ValidationResult> ValidateExtractedDataAsync(ExtractedData data, CancellationToken ct = default);
}

/// <summary>
/// Encryption / DRM / antivirus port. Mirrors Java <c>DocumentSecurityPort</c>.
/// </summary>
public interface IDocumentSecurityPort
{
    Task<bool> ScanAsync(DocumentProcessingRequest request, CancellationToken ct = default);
    Task<Stream> EncryptAsync(Stream content, CancellationToken ct = default);
    Task<Stream> DecryptAsync(Stream encrypted, CancellationToken ct = default);
}
