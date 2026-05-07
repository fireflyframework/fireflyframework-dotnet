namespace FireflyFramework.Ecm.Adapters;

/// <summary>
/// Capabilities an ECM adapter implementation declares. <see cref="FlagsAttribute"/> so
/// adapters can OR together every feature they support. Mirrors Java
/// <c>AdapterFeature</c>.
/// </summary>
[Flags]
public enum AdapterFeature : long
{
    None                       = 0,

    DocumentCrud               = 1L << 0,
    ContentStorage             = 1L << 1,
    Versioning                 = 1L << 2,
    FolderManagement           = 1L << 3,
    FolderHierarchy            = 1L << 4,
    Permissions                = 1L << 5,
    Security                   = 1L << 6,
    Search                     = 1L << 7,
    MetadataSearch             = 1L << 8,
    AuditTrail                 = 1L << 9,
    ESignatureEnvelopes        = 1L << 10,
    ESignatureRequests         = 1L << 11,
    SignatureValidation        = 1L << 12,
    SignatureProof             = 1L << 13,
    Encryption                 = 1L << 14,
    Streaming                  = 1L << 15,
    BatchOperations            = 1L << 16,
    Transactions               = 1L << 17,
    BackupRestore              = 1L << 18,
    MultiTenancy               = 1L << 19,
    CloudStorage               = 1L << 20,
    EnterpriseEcm              = 1L << 21,
    Compliance                 = 1L << 22,
    LegalHold                  = 1L << 23,
    RetentionPolicies          = 1L << 24,
    TextExtraction             = 1L << 25,
    Ocr                        = 1L << 26,
    DocumentClassification     = 1L << 27,
    DocumentValidation         = 1L << 28,
    StructuredDataExtraction   = 1L << 29,
    FormProcessing             = 1L << 30,
    TableExtraction            = 1L << 31,
    HandwritingRecognition     = 1L << 32,
    LayoutAnalysis             = 1L << 33,
    EntityExtraction           = 1L << 34,
    QualityAssessment          = 1L << 35,
    TemplateExtraction         = 1L << 36,
    IntelligentDocumentProcessing = 1L << 37,
}

/// <summary>
/// Marks an ECM adapter for runtime registration. Mirrors Java <c>@EcmAdapter</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class EcmAdapterAttribute : Attribute
{
    public EcmAdapterAttribute(string type) => Type = type;
    public string Type { get; }
    public string? Description { get; set; }
    public int Priority { get; set; }
    public bool Enabled { get; set; } = true;
    public AdapterFeature SupportedFeatures { get; set; }
    public string[] RequiredProperties { get; set; } = Array.Empty<string>();
    public string[] OptionalProperties { get; set; } = Array.Empty<string>();
}

/// <summary>Adapter validation result. Mirrors Java <c>AdapterValidationResult</c>.</summary>
public sealed record AdapterValidationResult(bool IsValid, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings)
{
    public static AdapterValidationResult Valid() => new(true, Array.Empty<string>(), Array.Empty<string>());
    public static AdapterValidationResult Invalid(params string[] errors) => new(false, errors, Array.Empty<string>());
}

/// <summary>Static metadata describing a registered adapter. Mirrors Java <c>AdapterInfo</c>.</summary>
public sealed record AdapterInfo(
    string Type,
    string Description,
    int Priority,
    bool Enabled,
    AdapterFeature SupportedFeatures,
    IReadOnlyList<string> RequiredProperties,
    IReadOnlyList<string> OptionalProperties,
    Type ImplementationType);

/// <summary>Operational profile for an adapter. Mirrors Java <c>AdapterProfile</c>.</summary>
public enum AdapterProfile
{
    NoOp,        // discards everything
    Local,       // in-memory / single-node
    Cloud,       // managed cloud service
    Enterprise,  // on-prem enterprise ECM
}
