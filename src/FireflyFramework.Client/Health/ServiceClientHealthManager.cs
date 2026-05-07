using System.Collections.Concurrent;

namespace FireflyFramework.Client.Health;

/// <summary>
/// Tracks per-service liveness signals. Mirrors Java <c>ServiceClientHealthManager</c>.
/// The framework records every successful or failed call against a service via
/// <see cref="RecordSuccess"/> / <see cref="RecordFailure"/>; <see cref="Get"/> returns the
/// per-service state and <see cref="GetOverall"/> rolls up to a single
/// <see cref="OverallHealthState"/> snapshot for an actuator-style health endpoint.
/// </summary>
public sealed class ServiceClientHealthManager
{
    private readonly ConcurrentDictionary<string, ServiceHealthEntry> _services = new();

    /// <summary>Number of consecutive failures before a service flips to <see cref="ServiceHealthState.Unhealthy"/>.</summary>
    public int FailureThreshold { get; init; } = 3;

    /// <summary>Number of consecutive failures before a service flips to <see cref="ServiceHealthState.Degraded"/>.</summary>
    public int DegradedThreshold { get; init; } = 1;

    public void RecordSuccess(string serviceName)
    {
        _services.AddOrUpdate(serviceName,
            _ => new ServiceHealthEntry(ServiceHealthState.Healthy, 0, DateTimeOffset.UtcNow, "OK"),
            (_, _) => new ServiceHealthEntry(ServiceHealthState.Healthy, 0, DateTimeOffset.UtcNow, "OK"));
    }

    public void RecordFailure(string serviceName, string? message = null)
    {
        _services.AddOrUpdate(serviceName,
            _ => new ServiceHealthEntry(StateFor(1), 1, DateTimeOffset.UtcNow, message),
            (_, prev) =>
            {
                var failures = prev.ConsecutiveFailures + 1;
                return new ServiceHealthEntry(StateFor(failures), failures, DateTimeOffset.UtcNow, message);
            });
    }

    public ServiceHealthEntry? Get(string serviceName) =>
        _services.TryGetValue(serviceName, out var entry) ? entry : null;

    public OverallHealth GetOverall()
    {
        var snapshot = _services.ToDictionary(p => p.Key, p => p.Value);
        var healthy = snapshot.Count(p => p.Value.State == ServiceHealthState.Healthy);
        var degraded = snapshot.Count(p => p.Value.State == ServiceHealthState.Degraded);
        var unhealthy = snapshot.Count(p => p.Value.State == ServiceHealthState.Unhealthy);
        var overall = unhealthy > 0
            ? OverallHealthState.Down
            : degraded > 0
                ? OverallHealthState.OutOfService
                : OverallHealthState.Up;
        return new OverallHealth(overall, snapshot.Count, healthy, degraded, unhealthy, snapshot);
    }

    private ServiceHealthState StateFor(int consecutiveFailures) => consecutiveFailures switch
    {
        var n when n >= FailureThreshold => ServiceHealthState.Unhealthy,
        var n when n >= DegradedThreshold => ServiceHealthState.Degraded,
        _ => ServiceHealthState.Healthy,
    };
}

/// <summary>Health state of one service.</summary>
public enum ServiceHealthState { Healthy, Degraded, Unhealthy }

/// <summary>Aggregate health state.</summary>
public enum OverallHealthState { Up, OutOfService, Down }

/// <summary>Per-service health snapshot.</summary>
public sealed record ServiceHealthEntry(
    ServiceHealthState State,
    int ConsecutiveFailures,
    DateTimeOffset LastCheckedAt,
    string? Message);

/// <summary>Aggregate health snapshot, suitable for an actuator-style endpoint.</summary>
public sealed record OverallHealth(
    OverallHealthState State,
    int TotalServices,
    int HealthyServices,
    int DegradedServices,
    int UnhealthyServices,
    IReadOnlyDictionary<string, ServiceHealthEntry> Services);
