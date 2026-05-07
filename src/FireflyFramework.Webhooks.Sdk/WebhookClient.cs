using System.Net.Http.Json;
using FireflyFramework.Webhooks.Interfaces;

namespace FireflyFramework.Webhooks.Sdk;

public sealed class WebhookClient
{
    private readonly HttpClient _http;
    public WebhookClient(HttpClient http) => _http = http;

    public async Task<WebhookResponseDto?> SendAsync(string provider, object payload, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"webhooks/{provider}", payload, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<WebhookResponseDto>(cancellationToken: ct).ConfigureAwait(false);
    }
}
