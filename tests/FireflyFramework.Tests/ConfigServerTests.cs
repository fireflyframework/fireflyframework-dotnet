using System.Net;
using System.Net.Http.Json;
using FireflyFramework.ConfigServer;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace FireflyFramework.Tests;

public class ConfigServerTests : IDisposable
{
    private readonly string _dir;
    private readonly IHost _host;

    public ConfigServerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"firefly-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "orders-prod.yml"), "spring.profile: prod\nfoo: bar\n");
        File.WriteAllText(Path.Combine(_dir, "application.json"), "{\"shared\":\"value\"}");

        _host = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureAppConfiguration(cfg => cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Firefly:ConfigServer:SearchDirectory"] = _dir,
                }));
                web.ConfigureServices((ctx, s) =>
                {
                    s.AddRouting();
                    s.AddFireflyConfigServer(ctx.Configuration);
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(e => e.MapFireflyConfigServer());
                });
            })
            .Start();
    }

    [Fact]
    public async Task Health_endpoint_returns_up()
    {
        using var client = _host.GetTestClient();
        var resp = await client.GetAsync("/health");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task App_profile_endpoint_returns_envelope_and_property_sources()
    {
        using var client = _host.GetTestClient();
        var resp = await client.GetFromJsonAsync<ConfigEnvelope>("/orders/prod");

        resp.Should().NotBeNull();
        resp!.name.Should().Be("orders");
        resp.profiles.Should().BeEquivalentTo(new[] { "prod" });
        resp.propertySources.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Falls_back_to_application_when_no_app_specific_file_exists()
    {
        using var client = _host.GetTestClient();
        var resp = await client.GetFromJsonAsync<ConfigEnvelope>("/unknown-app/dev");

        resp.Should().NotBeNull();
        resp!.propertySources.Should().NotBeEmpty();           // application.json fallback
        resp.propertySources!.First().name.Should().Contain("application.json");
    }

    public void Dispose()
    {
        _host.Dispose();
        try { Directory.Delete(_dir, recursive: true); } catch { /* best-effort */ }
    }

    private sealed record ConfigEnvelope(string name, string[] profiles, string? label, IList<PropertySource>? propertySources);
    private sealed record PropertySource(string name);
}
