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

using System.Net.Http.Json;

namespace FireflyFramework.Client.Rest;

/// <summary>
/// High-level wrapper over an <see cref="HttpClient"/> that returns deserialised
/// payloads. Use it when the calling code wants a strongly-typed surface; for raw
/// access, the underlying <see cref="HttpClient"/> built via <see cref="RestClientBuilder"/>
/// is sufficient. Mirrors Java <c>RestClient</c>.
/// </summary>
public interface IRestClient
{
    Task<T?> GetAsync<T>(string path, CancellationToken ct = default);
    Task<T?> PostAsync<T>(string path, object body, CancellationToken ct = default);
    Task<T?> PutAsync<T>(string path, object body, CancellationToken ct = default);
    Task<T?> PatchAsync<T>(string path, object body, CancellationToken ct = default);
    Task<bool> DeleteAsync(string path, CancellationToken ct = default);
}

public sealed class HttpRestClient : IRestClient
{
    private readonly HttpClient _http;
    public HttpRestClient(HttpClient http) => _http = http;

    public Task<T?> GetAsync<T>(string path, CancellationToken ct = default) =>
        _http.GetFromJsonAsync<T>(path, ct);

    public async Task<T?> PostAsync<T>(string path, object body, CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync(path, body, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<T>(cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task<T?> PutAsync<T>(string path, object body, CancellationToken ct = default)
    {
        using var resp = await _http.PutAsJsonAsync(path, body, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<T>(cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task<T?> PatchAsync<T>(string path, object body, CancellationToken ct = default)
    {
        using var resp = await _http.PatchAsJsonAsync(path, body, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<T>(cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(string path, CancellationToken ct = default)
    {
        using var resp = await _http.DeleteAsync(path, ct).ConfigureAwait(false);
        return resp.IsSuccessStatusCode;
    }
}
