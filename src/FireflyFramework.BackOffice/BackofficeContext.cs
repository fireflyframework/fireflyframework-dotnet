namespace FireflyFramework.BackOffice;

/// <summary>
/// Immutable context for back-office requests with customer impersonation. Mirrors Java
/// <c>BackofficeContext</c>: identifies the back-office user performing the action, the
/// customer (party) being impersonated, business identifiers (contract / product), and
/// the role/permission sets used for authorisation.
/// </summary>
public sealed record BackofficeContext(
    Guid BackofficeUserId,
    Guid ImpersonatedPartyId,
    Guid? ContractId = null,
    Guid? ProductId = null,
    IReadOnlySet<string>? BackofficeRoles = null,
    IReadOnlySet<string>? BackofficePermissions = null,
    IReadOnlySet<string>? ImpersonatedPartyRoles = null,
    IReadOnlySet<string>? ImpersonatedPartyPermissions = null,
    Guid? TenantId = null,
    DateTimeOffset? ImpersonationStartedAt = null,
    string? ImpersonationReason = null,
    string? BackofficeUserIpAddress = null,
    IReadOnlyDictionary<string, object?>? Attributes = null)
{
    public bool HasBackofficeRole(string role) => BackofficeRoles?.Contains(role) == true;

    public bool HasAnyBackofficeRole(params string[] roles) =>
        BackofficeRoles is not null && roles.Any(BackofficeRoles.Contains);

    public bool HasAllBackofficeRoles(params string[] roles) =>
        BackofficeRoles is not null && roles.All(BackofficeRoles.Contains);

    public bool HasBackofficePermission(string permission) =>
        BackofficePermissions?.Contains(permission) == true;

    public bool ImpersonatedPartyHasRole(string role) =>
        ImpersonatedPartyRoles?.Contains(role) == true;

    public bool HasContract() => ContractId.HasValue;
    public bool HasProduct() => ProductId.HasValue;

    public T? GetAttribute<T>(string key) =>
        Attributes is not null && Attributes.TryGetValue(key, out var v) && v is T t ? t : default;

    public bool IsValidImpersonation() => BackofficeUserId != Guid.Empty && ImpersonatedPartyId != Guid.Empty;
}
