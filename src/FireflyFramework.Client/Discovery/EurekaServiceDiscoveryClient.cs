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
/// Eureka-backed <see cref="IServiceDiscoveryClient"/>. Mirrors Java
/// <c>EurekaServiceDiscoveryClient</c>; talks the Eureka REST API:
///
/// <list type="bullet">
/// <item><c>GET    /eureka/apps/{APP}</c> — list instances of an application.</item>
/// <item><c>POST   /eureka/apps/{APP}</c> — register an instance.</item>
/// <item><c>DELETE /eureka/apps/{APP}/{instanceId}</c> — deregister an instance.</item>
/// </list>
///
/// <para>Eureka serialises XML port elements as JSON with two fields — <c>$</c> for the
/// port number and <c>@enabled</c> for the boolean flag. The DTOs below model that wire
/// shape. App names are upper-cased on the wire (Eureka's convention).</para>
///
/// <para>Failures contacting Eureka are caught and logged: <see cref="GetInstancesAsync"/>
/// yields nothing; <see cref="ResolveEndpointAsync"/> falls back to a name-based
/// <c>http://service-name</c> URI so callers can retry against the next registry.</para>
/// </summary>
public sealed class EurekaServiceDiscoveryClient : IServiceDiscoveryClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public EurekaServiceDiscoveryClient(HttpClient http, string eurekaUrl)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        ArgumentException.ThrowIfNullOrEmpty(eurekaUrl);
        _baseUrl = eurekaUrl.TrimEnd('/');
    }

    public async Task<string> ResolveEndpointAsync(string serviceName, CancellationToken ct = default)
    {
        var healthy = await GetHealthyInstanceAsync(serviceName, ct).ConfigureAwait(false);
        return healthy?.Uri ?? $"http://{serviceName.ToLowerInvariant()}";
    }

    public async IAsyncEnumerable<ServiceInstance> GetInstancesAsync(string serviceName, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        EurekaApplicationResponse? body;
        try
        {
            using var resp = await _http.GetAsync($"{_baseUrl}/eureka/apps/{serviceName.ToUpperInvariant()}", ct).ConfigureAwait(false);
            if (resp.StatusCode == HttpStatusCode.NotFound) yield break;
            resp.EnsureSuccessStatusCode();
            body = await resp.Content.ReadFromJsonAsync<EurekaApplicationResponse>(JsonOpts, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException) { yield break; }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { yield break; }

        if (body?.Application?.Instance is null) yield break;

        foreach (var inst in body.Application.Instance)
        {
            var port = inst.Port?.Value ?? 8080;
            var secure = inst.SecurePort?.Enabled == true;
            var status = string.Equals(inst.Status, "UP", StringComparison.OrdinalIgnoreCase) ? HealthStatus.Up : HealthStatus.Down;
            yield return new ServiceInstance(
                inst.InstanceId ?? $"{inst.HostName}-{port}",
                serviceName,
                inst.HostName ?? string.Empty,
                secure ? (inst.SecurePort?.Value ?? port) : port,
                secure,
                status,
                inst.Metadata ?? new Dictionary<string, string>());
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
        var body = new EurekaRegistrationRequest
        {
            Instance = new EurekaInstanceInfo
            {
                InstanceId = instance.InstanceId,
                App = instance.ServiceName.ToUpperInvariant(),
                HostName = instance.Host,
                IpAddr = instance.Host,
                Status = "UP",
                Port = new EurekaPort { Value = instance.Port, Enabled = !instance.Secure },
                SecurePort = new EurekaPort { Value = instance.Port, Enabled = instance.Secure },
                Metadata = instance.Metadata.ToDictionary(p => p.Key, p => p.Value),
            },
        };

        using var resp = await _http.PostAsJsonAsync(
            $"{_baseUrl}/eureka/apps/{instance.ServiceName.ToUpperInvariant()}",
            body, JsonOpts, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }

    public async Task DeregisterAsync(string instanceId, CancellationToken ct = default)
    {
        // Eureka instance IDs are typically `hostname:appName:port`. Recover the app name from
        // the colon-separated form; fall back to the raw id when the format isn't recognisable.
        var parts = instanceId.Split(':');
        var appName = parts.Length > 1 ? parts[1] : instanceId;
        using var resp = await _http.DeleteAsync(
            $"{_baseUrl}/eureka/apps/{appName.ToUpperInvariant()}/{instanceId}", ct).ConfigureAwait(false);
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

    private sealed class EurekaApplicationResponse
    {
        [JsonPropertyName("application")] public EurekaApplication? Application { get; set; }
    }

    private sealed class EurekaApplication
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("instance")] public List<EurekaInstanceInfo>? Instance { get; set; }
    }

    private sealed class EurekaInstanceInfo
    {
        [JsonPropertyName("instanceId")] public string? InstanceId { get; set; }
        [JsonPropertyName("app")] public string? App { get; set; }
        [JsonPropertyName("hostName")] public string? HostName { get; set; }
        [JsonPropertyName("ipAddr")] public string? IpAddr { get; set; }
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("port")] public EurekaPort? Port { get; set; }
        [JsonPropertyName("securePort")] public EurekaPort? SecurePort { get; set; }
        [JsonPropertyName("metadata")] public Dictionary<string, string>? Metadata { get; set; }
    }

    private sealed class EurekaPort
    {
        [JsonPropertyName("$")] public int Value { get; set; }
        [JsonPropertyName("@enabled")] public bool Enabled { get; set; }
    }

    private sealed class EurekaRegistrationRequest
    {
        [JsonPropertyName("instance")] public EurekaInstanceInfo? Instance { get; set; }
    }
}
