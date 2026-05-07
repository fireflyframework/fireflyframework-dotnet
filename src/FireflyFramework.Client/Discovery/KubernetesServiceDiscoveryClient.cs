// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace FireflyFramework.Client.Discovery;

/// <summary>
/// Kubernetes-style DNS <see cref="IServiceDiscoveryClient"/>. Mirrors Java
/// <c>KubernetesServiceDiscoveryClient</c>. In a Kubernetes cluster, every
/// <c>Service</c> object is reachable at the deterministic DNS name
/// <c>{serviceName}.{namespace}.svc.cluster.local</c>; this client returns that name
/// without ever calling the Kubernetes API.
///
/// <para>Registration is a no-op — Kubernetes registers / deregisters services itself
/// when pods start and stop. Health is reported as <see cref="HealthStatus.Up"/> because
/// the cluster's own readiness gates already filter unhealthy endpoints out of the
/// service's <c>Endpoints</c> set.</para>
///
/// <para>For pod-level discovery (introspecting endpoints, watching changes), use the
/// official <c>KubernetesClient</c> NuGet directly; this adapter is the lightweight
/// "talk to the service VIP" path that suits 95% of consumer applications.</para>
/// </summary>
public sealed class KubernetesServiceDiscoveryClient : IServiceDiscoveryClient
{
    private readonly string _namespace;
    private readonly string _clusterDomain;
    private readonly bool _secure;
    private readonly int _port;

    public KubernetesServiceDiscoveryClient(
        string @namespace = "default",
        string clusterDomain = "cluster.local",
        bool secure = false,
        int port = 80)
    {
        ArgumentException.ThrowIfNullOrEmpty(@namespace);
        ArgumentException.ThrowIfNullOrEmpty(clusterDomain);
        if (port is <= 0 or > 65_535) throw new ArgumentOutOfRangeException(nameof(port));

        _namespace = @namespace;
        _clusterDomain = clusterDomain;
        _secure = secure;
        _port = port;
    }

    public Task<string> ResolveEndpointAsync(string serviceName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(serviceName);
        var scheme = _secure ? "https" : "http";
        return Task.FromResult($"{scheme}://{serviceName}.{_namespace}.svc.{_clusterDomain}:{_port}");
    }

    public async IAsyncEnumerable<ServiceInstance> GetInstancesAsync(string serviceName, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(serviceName);
        yield return new ServiceInstance(
            InstanceId: $"{serviceName}-vip",
            ServiceName: serviceName,
            Host: $"{serviceName}.{_namespace}.svc.{_clusterDomain}",
            Port: _port,
            Secure: _secure,
            HealthStatus: HealthStatus.Up,
            Metadata: new Dictionary<string, string>
            {
                ["namespace"] = _namespace,
                ["clusterDomain"] = _clusterDomain,
            });
        await Task.CompletedTask;
    }

    public async Task<ServiceInstance?> GetHealthyInstanceAsync(string serviceName, CancellationToken ct = default)
    {
        await foreach (var inst in GetInstancesAsync(serviceName, ct).ConfigureAwait(false))
        {
            return inst;
        }
        return null;
    }

    /// <summary>No-op — Kubernetes manages registration itself.</summary>
    public Task RegisterAsync(ServiceInstance instance, CancellationToken ct = default) => Task.CompletedTask;

    /// <summary>No-op — Kubernetes manages deregistration itself.</summary>
    public Task DeregisterAsync(string instanceId, CancellationToken ct = default) => Task.CompletedTask;

    /// <summary>
    /// Always <c>true</c> — DNS-based discovery has no negative-response shape; if the
    /// service doesn't exist the eventual <c>HttpClient</c> request will fail at connect
    /// time. Caller-side health checks should be the source of truth here.
    /// </summary>
    public Task<bool> IsServiceAvailableAsync(string serviceName, CancellationToken ct = default) =>
        Task.FromResult(true);
}
