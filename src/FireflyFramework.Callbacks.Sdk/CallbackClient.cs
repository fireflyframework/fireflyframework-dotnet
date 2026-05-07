using System.Net.Http.Json;
using FireflyFramework.Callbacks.Interfaces;

namespace FireflyFramework.Callbacks.Sdk;

public sealed class CallbackClient
{
    private readonly HttpClient _http;
    public CallbackClient(HttpClient http) => _http = http;

    public Task<IReadOnlyList<CallbackConfigurationDto>?> ListAsync(CancellationToken ct = default) =>
        _http.GetFromJsonAsync<IReadOnlyList<CallbackConfigurationDto>>("api/callbacks/configurations", ct);

    public async Task<CallbackConfigurationDto?> CreateAsync(CallbackConfigurationDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/callbacks/configurations", dto, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<CallbackConfigurationDto>(cancellationToken: ct).ConfigureAwait(false);
    }
}
