using FireflyFramework.Callbacks.Interfaces;
using FireflyFramework.Data.Domain;

namespace FireflyFramework.Callbacks.Models;

public sealed class CallbackConfigurationEntity : BaseEntity<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public CallbackHttpMethod HttpMethod { get; set; } = CallbackHttpMethod.Post;
    public CallbackStatus Status { get; set; } = CallbackStatus.Active;
    public string SubscribedEventTypesJson { get; set; } = "[]";
    public string? CustomHeadersJson { get; set; }
    public string? Secret { get; set; }
    public bool SignatureEnabled { get; set; }
    public string? SignatureHeader { get; set; }
    public int MaxRetries { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 1000;
    public double RetryBackoffMultiplier { get; set; } = 2.0;
    public int TimeoutMs { get; set; } = 30_000;
    public bool Active { get; set; } = true;
    public string? TenantId { get; set; }
    public string? FilterExpression { get; set; }
    public string? MetadataJson { get; set; }
    public int FailureThreshold { get; set; } = 10;
    public int FailureCount { get; set; }
    public DateTimeOffset? LastSuccessAt { get; set; }
    public DateTimeOffset? LastFailureAt { get; set; }
}

public sealed class AuthorizedDomainEntity : BaseEntity<Guid>
{
    public string Domain { get; set; } = string.Empty;
    public string? AllowedIPsJson { get; set; }
    public bool IsAuthorized { get; set; } = true;
}

public sealed class EventSubscriptionEntity : BaseEntity<Guid>
{
    public Guid ConfigurationId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class CallbackExecutionEntity : BaseEntity<Guid>
{
    public Guid ConfigurationId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string SourceEventId { get; set; } = string.Empty;
    public CallbackExecutionStatus Status { get; set; }
    public string? RequestPayload { get; set; }
    public string? RequestHeaders { get; set; }
    public int? ResponseStatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public int AttemptNumber { get; set; }
    public int MaxAttempts { get; set; }
    public long RequestDurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset ExecutedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
