using System.Collections.Concurrent;

namespace FireflyFramework.Client.Discovery;

/// <summary>
/// Static configuration-based <see cref="IServiceDiscoveryClient"/>. The map of
/// <c>serviceName → endpoint URLs</c> is fixed at construction time. Used in tests, in
/// stand-alone deployments, and as the default fallback when no registry is configured.
/// Mirrors Java <c>StaticServiceDiscoveryClient</c>.
/// </summary>
public sealed class StaticServiceDiscoveryClient : IServiceDiscoveryClient
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _endpoints;

    public StaticServiceDiscoveryClient(IReadOnlyDictionary<string, IReadOnlyList<string>> serviceEndpoints)
    {
        ArgumentNullException.ThrowIfNull(serviceEndpoints);
        _endpoints = new ConcurrentDictionary<string, IReadOnlyList<string>>(serviceEndpoints);
    }

    public Task<string> ResolveEndpointAsync(string serviceName, CancellationToken ct = default)
    {
        if (_endpoints.TryGetValue(serviceName, out var list) && list.Count > 0)
        {
            return Task.FromResult(list[0]);
        }
        throw new ArgumentException($"No endpoints registered for service '{serviceName}'.", nameof(serviceName));
    }

    public async IAsyncEnumerable<ServiceInstance> GetInstancesAsync(string serviceName, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!_endpoints.TryGetValue(serviceName, out var endpoints)) yield break;
        foreach (var endpoint in endpoints)
        {
            yield return Parse(serviceName, endpoint);
        }
        await Task.CompletedTask;
    }

    public async Task<ServiceInstance?> GetHealthyInstanceAsync(string serviceName, CancellationToken ct = default)
    {
        await foreach (var inst in GetInstancesAsync(serviceName, ct).ConfigureAwait(false))
        {
            if (inst.IsHealthy) return inst;
        }
        return null;
    }

    public Task RegisterAsync(ServiceInstance instance, CancellationToken ct = default) => Task.CompletedTask;
    public Task DeregisterAsync(string instanceId, CancellationToken ct = default) => Task.CompletedTask;

    public Task<bool> IsServiceAvailableAsync(string serviceName, CancellationToken ct = default) =>
        Task.FromResult(_endpoints.TryGetValue(serviceName, out var list) && list.Count > 0);

    private static ServiceInstance Parse(string serviceName, string endpoint)
    {
        var secure = endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        var hostPort = endpoint.Replace("https://", string.Empty, StringComparison.OrdinalIgnoreCase)
                               .Replace("http://", string.Empty, StringComparison.OrdinalIgnoreCase)
                               .TrimEnd('/');
        var parts = hostPort.Split(':', 2);
        var host = parts[0];
        var port = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : (secure ? 443 : 80);

        return new ServiceInstance(
            InstanceId: $"{serviceName}-{host}-{port}",
            ServiceName: serviceName,
            Host: host,
            Port: port,
            Secure: secure,
            HealthStatus: HealthStatus.Up,
            Metadata: new Dictionary<string, string> { ["type"] = "static" });
    }
}
