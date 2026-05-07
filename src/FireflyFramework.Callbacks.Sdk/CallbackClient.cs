using System.Net;
using System.Net.Http.Json;
using FireflyFramework.Callbacks.Interfaces;

namespace FireflyFramework.Callbacks.Sdk;

public sealed class CallbackClient : ICallbackClient
{
    private readonly HttpClient _http;

    public CallbackClient(HttpClient http) => _http = http;

    public Task<IReadOnlyList<CallbackConfigurationDto>?> ListAsync(string? tenantId = null, CancellationToken ct = default)
    {
        var path = string.IsNullOrWhiteSpace(tenantId)
            ? "api/callbacks/configurations"
            : $"api/callbacks/configurations?tenantId={Uri.EscapeDataString(tenantId)}";
        return _http.GetFromJsonAsync<IReadOnlyList<CallbackConfigurationDto>>(path, ct);
    }

    public async Task<CallbackConfigurationDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync($"api/callbacks/configurations/{id}", ct).ConfigureAwait(false);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<CallbackConfigurationDto>(cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task<CallbackConfigurationDto?> CreateAsync(CallbackConfigurationDto dto, CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync("api/callbacks/configurations", dto, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<CallbackConfigurationDto>(cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task<CallbackConfigurationDto?> UpdateAsync(Guid id, CallbackConfigurationDto dto, CancellationToken ct = default)
    {
        using var resp = await _http.PutAsJsonAsync($"api/callbacks/configurations/{id}", dto, ct).ConfigureAwait(false);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<CallbackConfigurationDto>(cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var resp = await _http.DeleteAsync($"api/callbacks/configurations/{id}", ct).ConfigureAwait(false);
        if (resp.StatusCode == HttpStatusCode.NotFound) return false;
        resp.EnsureSuccessStatusCode();
        return true;
    }
}
