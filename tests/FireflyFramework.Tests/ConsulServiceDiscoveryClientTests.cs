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
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace FireflyFramework.Tests;

/// <summary>
/// WireMock-driven tests for <see cref="ConsulServiceDiscoveryClient"/>. Stand up a fake
/// Consul HTTP API and verify the read path (<c>/v1/health/service/{name}?passing=true</c>),
/// register and deregister verbs, and the health-status mapping rules
/// (any non-passing check ⇒ <see cref="HealthStatus.Down"/>).
/// </summary>
public sealed class ConsulServiceDiscoveryClientTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly HttpClient _http;
    private readonly ConsulServiceDiscoveryClient _client;

    public ConsulServiceDiscoveryClientTests()
    {
        _server = WireMockServer.Start();
        _http = new HttpClient();
        _client = new ConsulServiceDiscoveryClient(_http, _server.Urls[0]);
    }

    [Fact]
    public async Task GetInstancesAsync_ParsesEntries_MapsAddressFromService()
    {
        _server.Given(Request.Create().UsingGet().WithPath("/v1/health/service/users"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                [
                  {
                    "Node": { "Address": "10.0.0.5" },
                    "Service": { "ID": "users-1", "Service": "users", "Address": "10.0.0.10", "Port": 8080, "Meta": { "zone": "eu" } },
                    "Checks": [ { "Status": "passing" } ]
                  }
                ]
                """));

        var instances = new List<ServiceInstance>();
        await foreach (var inst in _client.GetInstancesAsync("users")) instances.Add(inst);

        var only = Assert.Single(instances);
        Assert.Equal("users-1", only.InstanceId);
        Assert.Equal("10.0.0.10", only.Host);
        Assert.Equal(8080, only.Port);
        Assert.True(only.IsHealthy);
        Assert.Equal("eu", only.Metadata["zone"]);
    }

    [Fact]
    public async Task GetInstancesAsync_FallsBackToNodeAddress_WhenServiceAddressIsEmpty()
    {
        _server.Given(Request.Create().UsingGet().WithPath("/v1/health/service/users"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                [
                  {
                    "Node": { "Address": "10.0.0.5" },
                    "Service": { "ID": "users-1", "Service": "users", "Port": 8080, "Address": "" },
                    "Checks": [ { "Status": "passing" } ]
                  }
                ]
                """));

        var instances = new List<ServiceInstance>();
        await foreach (var inst in _client.GetInstancesAsync("users")) instances.Add(inst);

        Assert.Equal("10.0.0.5", instances[0].Host);
    }

    [Fact]
    public async Task GetInstancesAsync_AnyNonPassingCheck_MarksDown()
    {
        _server.Given(Request.Create().UsingGet().WithPath("/v1/health/service/users"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                [
                  {
                    "Node": { "Address": "10.0.0.5" },
                    "Service": { "ID": "users-1", "Service": "users", "Address": "10.0.0.5", "Port": 8080 },
                    "Checks": [ { "Status": "passing" }, { "Status": "warning" } ]
                  }
                ]
                """));

        var instances = new List<ServiceInstance>();
        await foreach (var inst in _client.GetInstancesAsync("users")) instances.Add(inst);

        Assert.False(instances[0].IsHealthy);
    }

    [Fact]
    public async Task GetInstancesAsync_HttpFailure_YieldsEmpty()
    {
        _server.Given(Request.Create().UsingGet().WithPath("/v1/health/service/users"))
            .RespondWith(Response.Create().WithStatusCode(500));

        var instances = new List<ServiceInstance>();
        await foreach (var inst in _client.GetInstancesAsync("users")) instances.Add(inst);

        Assert.Empty(instances);
    }

    [Fact]
    public async Task RegisterAsync_PutsRegistrationPayload_WithCapitalisedFields()
    {
        _server.Given(Request.Create()
                .UsingPut()
                .WithPath("/v1/agent/service/register")
                .WithBody(b => b?.Contains("\"ID\":\"users-1\"") == true && b.Contains("\"Name\":\"users\"")))
            .RespondWith(Response.Create().WithStatusCode(200));

        await _client.RegisterAsync(new ServiceInstance(
            "users-1", "users", "10.0.0.10", 8080, false, HealthStatus.Up,
            new Dictionary<string, string> { ["zone"] = "eu" }));
    }

    [Fact]
    public async Task DeregisterAsync_PutsDeregisterByServiceId()
    {
        _server.Given(Request.Create()
                .UsingPut()
                .WithPath("/v1/agent/service/deregister/users-1"))
            .RespondWith(Response.Create().WithStatusCode(200));

        await _client.DeregisterAsync("users-1");
    }

    [Fact]
    public async Task ResolveEndpointAsync_FallsBackToNameWhenNoneHealthy()
    {
        _server.Given(Request.Create().UsingGet().WithPath("/v1/health/service/users"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("[]"));

        Assert.Equal("http://users", await _client.ResolveEndpointAsync("users"));
    }

    public void Dispose()
    {
        _http.Dispose();
        _server.Dispose();
    }
}
