using System.Net;
using FireflyFramework.Client.Chaos;
using Xunit;

namespace FireflyFramework.Tests;

/// <summary>
/// Tests for <see cref="ChaosEngineeringHandler"/> + <see cref="FaultInjectionConfig"/>.
/// Probabilities are pinned to 1.0 so the rolls are deterministic; service filtering and
/// the disabled-by-default behaviour are pinned in their own cases.
/// </summary>
public sealed class ChaosEngineeringHandlerTests
{
    private static HttpClient NewClient(FaultInjectionConfig config)
    {
        var handler = new ChaosEngineeringHandler(config) { InnerHandler = new StubInnerHandler() };
        return new HttpClient(handler) { BaseAddress = new Uri("http://example.test/") };
    }

    [Fact]
    public async Task DisabledByDefault_PassesThrough()
    {
        var resp = await NewClient(new FaultInjectionConfig()).GetAsync("ping");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task ErrorInjection_AtProbability1_ThrowsChaosException()
    {
        var cfg = new FaultInjectionConfig
        {
            Enabled = true,
            ErrorInjectionEnabled = true,
            ErrorProbability = 1.0,
            ErrorFactory = () => new ChaosException("forced"),
        };

        await Assert.ThrowsAsync<ChaosException>(() => NewClient(cfg).GetAsync("ping"));
    }

    [Fact]
    public async Task LatencyInjection_AtProbability1_DelaysRequest()
    {
        var cfg = new FaultInjectionConfig
        {
            Enabled = true,
            LatencyInjectionEnabled = true,
            LatencyProbability = 1.0,
            MinLatency = TimeSpan.FromMilliseconds(80),
            MaxLatency = TimeSpan.FromMilliseconds(80),
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await NewClient(cfg).GetAsync("ping");
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds >= 70, $"expected ≥70 ms, got {sw.ElapsedMilliseconds}");
    }

    [Fact]
    public async Task IncludedServices_FilterByOption()
    {
        var cfg = new FaultInjectionConfig
        {
            Enabled = true,
            ErrorInjectionEnabled = true,
            ErrorProbability = 1.0,
            IncludedServices = new HashSet<string> { "users" },
        };
        var client = NewClient(cfg);

        // No service tag → falls back to host "example.test" → not in IncludedServices → no fault.
        var resp = await client.GetAsync("ping");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        // Tagged with the matched service name → fault is injected.
        var req = new HttpRequestMessage(HttpMethod.Get, "ping");
        req.Options.Set(new HttpRequestOptionsKey<string>(ChaosEngineeringHandler.ServiceNameOption), "users");
        await Assert.ThrowsAsync<ChaosException>(() => client.SendAsync(req));
    }

    [Fact]
    public async Task ExcludedServices_OverrideIncluded()
    {
        var cfg = new FaultInjectionConfig
        {
            Enabled = true,
            ErrorInjectionEnabled = true,
            ErrorProbability = 1.0,
            IncludedServices = new HashSet<string> { "users" },
            ExcludedServices = new HashSet<string> { "users" },
        };

        var req = new HttpRequestMessage(HttpMethod.Get, "ping");
        req.Options.Set(new HttpRequestOptionsKey<string>(ChaosEngineeringHandler.ServiceNameOption), "users");
        var resp = await NewClient(cfg).SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    private sealed class StubInnerHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
