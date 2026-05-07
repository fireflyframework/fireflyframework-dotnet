namespace FireflyFramework.EventSourcing.Tenancy;

/// <summary>
/// Ambient tenant id propagated through the async call chain. The Java implementation
/// uses Reactor's context; the .NET equivalent is <see cref="AsyncLocal{T}"/> which is
/// preserved across <c>async</c>/<c>await</c>, <c>Task.Run</c> and Channels.
/// </summary>
public static class TenantContext
{
    private static readonly AsyncLocal<string?> _current = new();

    public static string? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }

    public static IDisposable BeginScope(string tenantId)
    {
        var prior = _current.Value;
        _current.Value = tenantId;
        return new Scope(prior);
    }

    private sealed class Scope : IDisposable
    {
        private readonly string? _prior;
        public Scope(string? prior) => _prior = prior;
        public void Dispose() => _current.Value = _prior;
    }
}
