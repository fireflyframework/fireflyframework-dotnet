namespace FireflyFramework.Cqrs.Context;

/// <summary>Caller / request context. Mirrors Java <c>ExecutionContext</c>.</summary>
public sealed class ExecutionContext
{
    public string? UserId { get; init; }
    public string? TenantId { get; init; }
    public string? OrganizationId { get; init; }
    public string? SessionId { get; init; }
    public string? RequestId { get; init; }
    public string? Source { get; init; }
    public string? ClientIp { get; init; }
    public string? UserAgent { get; init; }
    public IReadOnlyDictionary<string, bool> FeatureFlags { get; init; } = new Dictionary<string, bool>();
    public IReadOnlyDictionary<string, object?> Properties { get; init; } = new Dictionary<string, object?>();
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();

    public static ExecutionContext Empty { get; } = new();
    public static ExecutionContext System { get; } = new() { UserId = "system", Source = "system" };
}
