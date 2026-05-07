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

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FireflyFramework.Client.Discovery;

/// <summary>
/// Consul-backed <see cref="IServiceDiscoveryClient"/>. Mirrors Java
/// <c>ConsulServiceDiscoveryClient</c>; talks Consul's HTTP agent / catalog API:
///
/// <list type="bullet">
/// <item><c>GET /v1/health/service/{name}?passing=true</c> — list passing instances.</item>
/// <item><c>PUT /v1/agent/service/register</c> — register an instance.</item>
/// <item><c>PUT /v1/agent/service/deregister/{id}</c> — deregister.</item>
/// </list>
///
/// <para>Consul returns each instance as a triple of <c>Node</c> / <c>Service</c> /
/// <c>Checks[]</c>. The instance is healthy iff every check has status <c>"passing"</c>;
/// otherwise it's marked <see cref="HealthStatus.Down"/>. The instance address falls back
/// to the node address when <c>Service.Address</c> is empty (Consul lets services run on
/// the same address as the node and just publish the port).</para>
///
/// <para>Network failures are swallowed (logged in production via the supplied
/// <see cref="HttpClient"/> handlers); <see cref="GetInstancesAsync"/> yields nothing.</para>
/// </summary>
public sealed class ConsulServiceDiscoveryClient : IServiceDiscoveryClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public ConsulServiceDiscoveryClient(HttpClient http, string consulUrl)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        ArgumentException.ThrowIfNullOrEmpty(consulUrl);
        _baseUrl = consulUrl.TrimEnd('/');
    }

    public async Task<string> ResolveEndpointAsync(string serviceName, CancellationToken ct = default)
    {
        var healthy = await GetHealthyInstanceAsync(serviceName, ct).ConfigureAwait(false);
        return healthy?.Uri ?? $"http://{serviceName.ToLowerInvariant()}";
    }

    public async IAsyncEnumerable<ServiceInstance> GetInstancesAsync(string serviceName, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        List<ConsulHealthEntry>? entries;
        try
        {
            using var resp = await _http.GetAsync($"{_baseUrl}/v1/health/service/{serviceName}?passing=true", ct).ConfigureAwait(false);
            if (resp.StatusCode == HttpStatusCode.NotFound) yield break;
            resp.EnsureSuccessStatusCode();
            entries = await resp.Content.ReadFromJsonAsync<List<ConsulHealthEntry>>(JsonOpts, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException) { yield break; }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { yield break; }

        if (entries is null) yield break;

        foreach (var entry in entries)
        {
            var svc = entry.Service;
            if (svc is null) continue;

            var status = HealthStatus.Up;
            if (entry.Checks is not null)
            {
                foreach (var check in entry.Checks)
                {
                    if (!string.Equals(check.Status, "passing", StringComparison.OrdinalIgnoreCase))
                    {
                        status = HealthStatus.Down;
                        break;
                    }
                }
            }

            var address = !string.IsNullOrEmpty(svc.Address) ? svc.Address : entry.Node?.Address ?? string.Empty;

            yield return new ServiceInstance(
                svc.Id ?? $"{serviceName}-{address}-{svc.Port}",
                serviceName,
                address,
                svc.Port,
                Secure: false,
                status,
                svc.Meta ?? new Dictionary<string, string>());
        }
    }

    public async Task<ServiceInstance?> GetHealthyInstanceAsync(string serviceName, CancellationToken ct = default)
    {
        await foreach (var inst in GetInstancesAsync(serviceName, ct).ConfigureAwait(false))
        {
            if (inst.IsHealthy) return inst;
        }
        return null;
    }

    public async Task RegisterAsync(ServiceInstance instance, CancellationToken ct = default)
    {
        var body = new ConsulServiceRegistration
        {
            Id = instance.InstanceId,
            Name = instance.ServiceName,
            Address = instance.Host,
            Port = instance.Port,
            Meta = instance.Metadata.ToDictionary(p => p.Key, p => p.Value),
        };

        using var req = new HttpRequestMessage(HttpMethod.Put, $"{_baseUrl}/v1/agent/service/register")
        {
            Content = JsonContent.Create(body, options: JsonOpts),
        };
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }

    public async Task DeregisterAsync(string instanceId, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Put, $"{_baseUrl}/v1/agent/service/deregister/{instanceId}");
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<bool> IsServiceAvailableAsync(string serviceName, CancellationToken ct = default)
    {
        await foreach (var _ in GetInstancesAsync(serviceName, ct).ConfigureAwait(false))
        {
            return true;
        }
        return false;
    }

    // ─── DTOs ──────────────────────────────────────────────────────

    private sealed class ConsulHealthEntry
    {
        [JsonPropertyName("Node")] public ConsulNode? Node { get; set; }
        [JsonPropertyName("Service")] public ConsulService? Service { get; set; }
        [JsonPropertyName("Checks")] public List<ConsulCheck>? Checks { get; set; }
    }

    private sealed class ConsulNode
    {
        [JsonPropertyName("Address")] public string? Address { get; set; }
    }

    private sealed class ConsulService
    {
        [JsonPropertyName("ID")] public string? Id { get; set; }
        [JsonPropertyName("Service")] public string? Name { get; set; }
        [JsonPropertyName("Address")] public string? Address { get; set; }
        [JsonPropertyName("Port")] public int Port { get; set; }
        [JsonPropertyName("Meta")] public Dictionary<string, string>? Meta { get; set; }
    }

    private sealed class ConsulCheck
    {
        [JsonPropertyName("Status")] public string? Status { get; set; }
    }

    private sealed class ConsulServiceRegistration
    {
        [JsonPropertyName("ID")] public string? Id { get; set; }
        [JsonPropertyName("Name")] public string? Name { get; set; }
        [JsonPropertyName("Address")] public string? Address { get; set; }
        [JsonPropertyName("Port")] public int Port { get; set; }
        [JsonPropertyName("Meta")] public Dictionary<string, string>? Meta { get; set; }
    }
}
