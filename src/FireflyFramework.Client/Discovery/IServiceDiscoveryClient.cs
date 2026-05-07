namespace FireflyFramework.Client.Discovery;

/// <summary>
/// Service discovery abstraction for dynamic endpoint resolution. Mirrors Java
/// <c>ServiceDiscoveryClient</c>. Implementations may consult a static map, DNS, the
/// Kubernetes API, Eureka, Consul, or any other registry.
/// </summary>
public interface IServiceDiscoveryClient
{
    /// <summary>Resolves a service endpoint URL (the first or load-balancer-selected instance).</summary>
    Task<string> ResolveEndpointAsync(string serviceName, CancellationToken ct = default);

    /// <summary>Streams every known instance of <paramref name="serviceName"/>.</summary>
    IAsyncEnumerable<ServiceInstance> GetInstancesAsync(string serviceName, CancellationToken ct = default);

    /// <summary>Returns the first healthy instance, or <c>null</c> if none are healthy.</summary>
    Task<ServiceInstance?> GetHealthyInstanceAsync(string serviceName, CancellationToken ct = default);

    /// <summary>Registers an instance with the registry (no-op for static / DNS implementations).</summary>
    Task RegisterAsync(ServiceInstance instance, CancellationToken ct = default);

    /// <summary>Deregisters an instance by id (no-op for static / DNS implementations).</summary>
    Task DeregisterAsync(string instanceId, CancellationToken ct = default);

    /// <summary>True if the service has at least one (not necessarily healthy) instance.</summary>
    Task<bool> IsServiceAvailableAsync(string serviceName, CancellationToken ct = default);
}

/// <summary>Represents a single instance of a service. Mirrors Java <c>ServiceInstance</c>.</summary>
public sealed record ServiceInstance(
    string InstanceId,
    string ServiceName,
    string Host,
    int Port,
    bool Secure,
    HealthStatus HealthStatus,
    IReadOnlyDictionary<string, string> Metadata)
{
    /// <summary>Returns <c>http(s)://host:port</c>.</summary>
    public string Uri => $"{(Secure ? "https" : "http")}://{Host}:{Port}";

    /// <summary>True iff <see cref="HealthStatus"/> is <see cref="HealthStatus.Up"/>.</summary>
    public bool IsHealthy => HealthStatus == HealthStatus.Up;
}

/// <summary>Health status of a service instance. Mirrors Java <c>ServiceDiscoveryClient.HealthStatus</c>.</summary>
public enum HealthStatus
{
    Up,
    Down,
    OutOfService,
    Unknown,
}

/// <summary>Discovery type discriminator. Mirrors Java <c>ServiceDiscoveryClient.ServiceDiscoveryType</c>.</summary>
public enum ServiceDiscoveryType
{
    Static,
    Dns,
    Kubernetes,
    Eureka,
    Consul,
}
