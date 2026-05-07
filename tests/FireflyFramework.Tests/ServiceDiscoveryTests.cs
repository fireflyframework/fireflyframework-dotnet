using FireflyFramework.Client.Discovery;
using Xunit;

namespace FireflyFramework.Tests;

/// <summary>Tests <see cref="StaticServiceDiscoveryClient"/> + the <see cref="ServiceInstance"/> record.</summary>
public sealed class ServiceDiscoveryTests
{
    private static StaticServiceDiscoveryClient NewClient() => new(new Dictionary<string, IReadOnlyList<string>>
    {
        ["users"] = new[] { "https://users-1.example.com:8443", "http://users-2.example.com:8080" },
        ["orders"] = new[] { "http://orders.example.com" },
    });

    [Fact]
    public async Task ResolveEndpointAsync_ReturnsFirstEndpoint()
    {
        var endpoint = await NewClient().ResolveEndpointAsync("users");
        Assert.Equal("https://users-1.example.com:8443", endpoint);
    }

    [Fact]
    public async Task ResolveEndpointAsync_UnknownService_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => NewClient().ResolveEndpointAsync("missing"));
    }

    [Fact]
    public async Task GetInstancesAsync_ParsesSchemeHostPort_AndDefaults()
    {
        var instances = new List<ServiceInstance>();
        await foreach (var inst in NewClient().GetInstancesAsync("users"))
        {
            instances.Add(inst);
        }

        Assert.Equal(2, instances.Count);
        Assert.Equal("users-1.example.com", instances[0].Host);
        Assert.Equal(8443, instances[0].Port);
        Assert.True(instances[0].Secure);
        Assert.Equal("users-2.example.com", instances[1].Host);
        Assert.Equal(8080, instances[1].Port);
        Assert.False(instances[1].Secure);
    }

    [Fact]
    public async Task GetInstancesAsync_EndpointWithoutPort_DefaultsTo80()
    {
        var instances = new List<ServiceInstance>();
        await foreach (var inst in NewClient().GetInstancesAsync("orders")) instances.Add(inst);

        var only = Assert.Single(instances);
        Assert.Equal(80, only.Port);
        Assert.False(only.Secure);
    }

    [Fact]
    public void ServiceInstance_Uri_FormatsByScheme()
    {
        var secure = new ServiceInstance("a", "s", "h", 443, true, HealthStatus.Up, new Dictionary<string, string>());
        Assert.Equal("https://h:443", secure.Uri);

        var insecure = secure with { Secure = false, Port = 80 };
        Assert.Equal("http://h:80", insecure.Uri);
    }

    [Fact]
    public async Task GetHealthyInstanceAsync_ReturnsFirstHealthy_OrNull()
    {
        var inst = await NewClient().GetHealthyInstanceAsync("users");
        Assert.NotNull(inst);
        Assert.True(inst!.IsHealthy);

        Assert.Null(await NewClient().GetHealthyInstanceAsync("does-not-exist"));
    }

    [Fact]
    public async Task IsServiceAvailableAsync_KnownService_ReturnsTrue()
    {
        Assert.True(await NewClient().IsServiceAvailableAsync("users"));
        Assert.False(await NewClient().IsServiceAvailableAsync("missing"));
    }
}
