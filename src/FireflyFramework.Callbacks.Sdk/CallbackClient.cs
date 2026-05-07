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
