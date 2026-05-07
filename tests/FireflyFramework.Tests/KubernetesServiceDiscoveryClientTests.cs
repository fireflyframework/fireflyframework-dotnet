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

using FireflyFramework.Client.Discovery;
using Xunit;

namespace FireflyFramework.Tests;

/// <summary>
/// Behavioural tests for <see cref="KubernetesServiceDiscoveryClient"/>. There's no
/// network involved — the client just synthesises the deterministic Kubernetes DNS name —
/// so each case asserts the resolved endpoint string directly.
/// </summary>
public sealed class KubernetesServiceDiscoveryClientTests
{
    [Fact]
    public async Task ResolveEndpointAsync_BuildsClusterLocalDns()
    {
        var client = new KubernetesServiceDiscoveryClient(@namespace: "default");
        var endpoint = await client.ResolveEndpointAsync("users");
        Assert.Equal("http://users.default.svc.cluster.local:80", endpoint);
    }

    [Fact]
    public async Task ResolveEndpointAsync_HonoursCustomNamespace_ClusterDomain_Port_AndScheme()
    {
        var client = new KubernetesServiceDiscoveryClient(
            @namespace: "production",
            clusterDomain: "k8s.local",
            secure: true,
            port: 8443);

        Assert.Equal("https://orders.production.svc.k8s.local:8443", await client.ResolveEndpointAsync("orders"));
    }

    [Fact]
    public async Task GetInstancesAsync_YieldsOneVipInstance_WithNamespaceMetadata()
    {
        var client = new KubernetesServiceDiscoveryClient(@namespace: "ns");
        var instances = new List<ServiceInstance>();
        await foreach (var inst in client.GetInstancesAsync("users")) instances.Add(inst);

        var only = Assert.Single(instances);
        Assert.Equal("users-vip", only.InstanceId);
        Assert.Equal("users.ns.svc.cluster.local", only.Host);
        Assert.Equal(80, only.Port);
        Assert.True(only.IsHealthy);
        Assert.Equal("ns", only.Metadata["namespace"]);
    }

    [Fact]
    public async Task RegisterAndDeregister_AreNoOps()
    {
        var client = new KubernetesServiceDiscoveryClient();
        await client.RegisterAsync(new ServiceInstance("a", "b", "c", 1, false, HealthStatus.Up, new Dictionary<string, string>()));
        await client.DeregisterAsync("anything");
    }

    [Fact]
    public async Task IsServiceAvailableAsync_AlwaysTrue()
    {
        var client = new KubernetesServiceDiscoveryClient();
        Assert.True(await client.IsServiceAvailableAsync("anything"));
    }

    [Fact]
    public void Constructor_RejectsInvalidPort_AndEmptyArgs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new KubernetesServiceDiscoveryClient(port: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new KubernetesServiceDiscoveryClient(port: 65_536));
        Assert.Throws<ArgumentException>(() => new KubernetesServiceDiscoveryClient(@namespace: ""));
        Assert.Throws<ArgumentException>(() => new KubernetesServiceDiscoveryClient(clusterDomain: ""));
    }
}
