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
