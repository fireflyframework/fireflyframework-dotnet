namespace FireflyFramework.Callbacks.Interfaces;

public enum CallbackStatus { Active, Paused, Disabled, Failed }
public enum CallbackExecutionStatus { Success, FailedRetrying, FailedPermanent }
public enum CallbackHttpMethod { Post, Put, Patch }

public sealed record CallbackConfigurationDto(
    Guid? Id,
    string Name,
    string Url,
    CallbackHttpMethod HttpMethod,
    CallbackStatus Status,
    string[] SubscribedEventTypes,
    Dictionary<string, string>? CustomHeaders,
    string? Secret,
    bool SignatureEnabled,
    string? SignatureHeader,
    int MaxRetries,
    int RetryDelayMs,
    double RetryBackoffMultiplier,
    int TimeoutMs,
    bool Active,
    string? TenantId,
    string? FilterExpression,
    Dictionary<string, object?>? Metadata,
    int FailureThreshold,
    int FailureCount,
    DateTimeOffset? LastSuccessAt,
    DateTimeOffset? LastFailureAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? CreatedBy,
    string? UpdatedBy);

public sealed record AuthorizedDomainDto(string Domain, string[]? AllowedIPs, bool IsAuthorized);
public sealed record EventSubscriptionDto(Guid ConfigurationId, string EventType, bool IsActive);

public sealed record CallbackExecutionDto(
    Guid Id,
    Guid ConfigurationId,
    string EventType,
    string SourceEventId,
    CallbackExecutionStatus Status,
    string? RequestPayload,
    string? RequestHeaders,
    int? ResponseStatusCode,
    string? ResponseBody,
    int AttemptNumber,
    int MaxAttempts,
    long RequestDurationMs,
    string? ErrorMessage,
    DateTimeOffset ExecutedAt,
    DateTimeOffset? CompletedAt);
