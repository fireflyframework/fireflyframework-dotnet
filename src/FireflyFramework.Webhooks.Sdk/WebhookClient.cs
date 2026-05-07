using System.Net.Http.Json;
using FireflyFramework.Webhooks.Interfaces;

namespace FireflyFramework.Webhooks.Sdk;

public sealed class WebhookClient : IWebhookClient
{
    private readonly HttpClient _http;

    public WebhookClient(HttpClient http) => _http = http;

    public async Task<WebhookResponseDto?> SendAsync(string provider, object payload, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        using var resp = await _http.PostAsJsonAsync($"api/webhooks/{Uri.EscapeDataString(provider)}", payload, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<WebhookResponseDto>(cancellationToken: ct).ConfigureAwait(false);
    }
}
