namespace FireflyFramework.BackOffice;

public enum SecurityConfigSource { Annotation, ExplicitMap, SecurityCenter, Default }

public sealed record SecurityEvaluationResult(
    bool Granted,
    string? Reason,
    string? EvaluatedPolicy,
    IReadOnlyDictionary<string, object?>? EvaluationDetails,
    DateTimeOffset EvaluatedAt);

public sealed record ImpersonationAuditTrail(
    Guid BackofficeUserId,
    Guid ImpersonatedPartyId,
    DateTimeOffset StartedAt,
    string? IpAddress = null,
    string? UserAgent = null,
    string? Reason = null,
    string? Endpoint = null,
    string? HttpMethod = null,
    string? SessionId = null,
    string? RequestId = null,
    IReadOnlyDictionary<string, object?>? Metadata = null);

/// <summary>
/// Endpoint-level security context for back-office routes. Mirrors Java
/// <c>BackofficeSecurityContext</c>: required roles/permissions, authorization outcome,
/// impersonation audit trail.
/// </summary>
public sealed record BackofficeSecurityContext(
    string Endpoint,
    string HttpMethod,
    IReadOnlySet<string>? RequiredBackofficeRoles = null,
    IReadOnlySet<string>? RequiredBackofficePermissions = null,
    bool ImpersonationAllowed = true,
    bool ImpersonationRequired = true,
    bool Authorized = false,
    string? AuthorizationFailureReason = null,
    Guid? BackofficeUserId = null,
    Guid? ImpersonatedPartyId = null,
    bool ImpersonationAuthorized = false,
    string? ImpersonationDenialReason = null,
    DateTimeOffset? ImpersonationAuthorizedAt = null,
    SecurityConfigSource ConfigSource = SecurityConfigSource.Default,
    IReadOnlyDictionary<string, object?>? SecurityAttributes = null,
    bool RequiresAuthentication = true,
    bool AllowAnonymous = false,
    SecurityEvaluationResult? EvaluationResult = null,
    ImpersonationAuditTrail? AuditTrail = null)
{
    public bool HasRequiredBackofficeRoles() => RequiredBackofficeRoles is { Count: > 0 };
    public bool HasRequiredBackofficePermissions() => RequiredBackofficePermissions is { Count: > 0 };

    public bool RequiresBackofficeRole(string role) =>
        RequiredBackofficeRoles?.Contains(role) == true;

    public bool RequiresBackofficePermission(string permission) =>
        RequiredBackofficePermissions?.Contains(permission) == true;

    public bool IsImpersonationValid() =>
        ImpersonationAllowed && ImpersonationAuthorized && ImpersonatedPartyId is not null;
}
