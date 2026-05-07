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
/// WireMock-driven tests for <see cref="EurekaServiceDiscoveryClient"/>. Stand up a fake
/// Eureka REST endpoint and verify the wire shape: the GET-by-app, the registration POST
/// payload, and the deregistration DELETE.
/// </summary>
public sealed class EurekaServiceDiscoveryClientTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly HttpClient _http;
    private readonly EurekaServiceDiscoveryClient _client;

    public EurekaServiceDiscoveryClientTests()
    {
        _server = WireMockServer.Start();
        _http = new HttpClient();
        _client = new EurekaServiceDiscoveryClient(_http, _server.Urls[0]);
    }

    [Fact]
    public async Task GetInstancesAsync_ParsesPortDollarSign_AndStatus()
    {
        _server.Given(Request.Create().UsingGet().WithPath("/eureka/apps/USERS"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "application": {
                    "name": "USERS",
                    "instance": [
                      {
                        "instanceId": "host-1:users:8080",
                        "hostName": "host-1",
                        "ipAddr": "10.0.0.1",
                        "status": "UP",
                        "port": { "$": 8080, "@enabled": true },
                        "securePort": { "$": 8443, "@enabled": false },
                        "metadata": { "zone": "eu-west-1a" }
                      },
                      {
                        "instanceId": "host-2:users:8080",
                        "hostName": "host-2",
                        "ipAddr": "10.0.0.2",
                        "status": "DOWN",
                        "port": { "$": 8080, "@enabled": true }
                      }
                    ]
                  }
                }
                """));

        var instances = new List<ServiceInstance>();
        await foreach (var inst in _client.GetInstancesAsync("users")) instances.Add(inst);

        Assert.Equal(2, instances.Count);
        Assert.Equal("host-1", instances[0].Host);
        Assert.Equal(8080, instances[0].Port);
        Assert.False(instances[0].Secure);
        Assert.True(instances[0].IsHealthy);
        Assert.Equal("eu-west-1a", instances[0].Metadata["zone"]);
        Assert.False(instances[1].IsHealthy);
    }

    [Fact]
    public async Task GetInstancesAsync_NotFound_YieldsEmpty()
    {
        _server.Given(Request.Create().UsingGet().WithPath("/eureka/apps/MISSING"))
            .RespondWith(Response.Create().WithStatusCode(404));

        var instances = new List<ServiceInstance>();
        await foreach (var inst in _client.GetInstancesAsync("missing")) instances.Add(inst);

        Assert.Empty(instances);
    }

    [Fact]
    public async Task ResolveEndpointAsync_ReturnsFirstHealthyInstance_OrFallback()
    {
        _server.Given(Request.Create().UsingGet().WithPath("/eureka/apps/USERS"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {"application":{"name":"USERS","instance":[
                    {"instanceId":"a","hostName":"host-1","status":"UP","port":{"$":8080,"@enabled":true}}
                ]}}
                """));

        var endpoint = await _client.ResolveEndpointAsync("users");
        Assert.Equal("http://host-1:8080", endpoint);

        // Unknown service falls back to name-based.
        _server.Given(Request.Create().UsingGet().WithPath("/eureka/apps/MISSING"))
            .RespondWith(Response.Create().WithStatusCode(404));
        Assert.Equal("http://missing", await _client.ResolveEndpointAsync("missing"));
    }

    [Fact]
    public async Task RegisterAsync_PostsInstancePayload()
    {
        _server.Given(Request.Create()
                .UsingPost()
                .WithPath("/eureka/apps/USERS")
                .WithBody(b => b?.Contains("\"hostName\":\"host-1\"") == true && b.Contains("\"app\":\"USERS\"")))
            .RespondWith(Response.Create().WithStatusCode(204));

        await _client.RegisterAsync(new ServiceInstance(
            "host-1:users:8080", "users", "host-1", 8080, false, HealthStatus.Up,
            new Dictionary<string, string>()));
    }

    [Fact]
    public async Task DeregisterAsync_DeletesByInstanceId_AndExtractsAppName()
    {
        _server.Given(Request.Create()
                .UsingDelete()
                .WithPath("/eureka/apps/USERS/host-1:users:8080"))
            .RespondWith(Response.Create().WithStatusCode(200));

        await _client.DeregisterAsync("host-1:users:8080");
    }

    [Fact]
    public async Task IsServiceAvailableAsync_ReflectsRegistryState()
    {
        _server.Given(Request.Create().UsingGet().WithPath("/eureka/apps/USERS"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"application":{"name":"USERS","instance":[{"instanceId":"a","hostName":"h","status":"UP","port":{"$":80,"@enabled":true}}]}}"""));

        Assert.True(await _client.IsServiceAvailableAsync("users"));

        _server.Given(Request.Create().UsingGet().WithPath("/eureka/apps/MISSING"))
            .RespondWith(Response.Create().WithStatusCode(404));
        Assert.False(await _client.IsServiceAvailableAsync("missing"));
    }

    public void Dispose()
    {
        _http.Dispose();
        _server.Dispose();
    }
}
