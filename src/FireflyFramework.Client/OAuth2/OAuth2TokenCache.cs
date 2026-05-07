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

using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace FireflyFramework.Client.OAuth2;

/// <summary>
/// Thread-safe OAuth2 token cache with on-demand refresh. Mirrors the caching layer of
/// Java <c>OAuth2ClientHelper</c>. Tokens are keyed by <c>(tokenUrl, clientId, scope)</c>
/// and refreshed automatically when within <see cref="RefreshSkew"/> of expiry.
/// </summary>
public sealed class OAuth2TokenCache
{
    private readonly HttpClient _http;
    private readonly ConcurrentDictionary<string, CachedToken> _tokens = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    /// <summary>
    /// Refresh skew — the cache forces a refresh when the cached token is within this
    /// distance of its absolute expiry, to avoid handing out tokens that are about to die
    /// in flight. Default: 30 seconds.
    /// </summary>
    public TimeSpan RefreshSkew { get; init; } = TimeSpan.FromSeconds(30);

    public OAuth2TokenCache(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    /// <summary>
    /// Returns a non-expired bearer token, fetching a new one via the OAuth2
    /// client_credentials grant if the cache is empty or near expiry.
    /// </summary>
    public async Task<string> GetTokenAsync(
        string tokenUrl,
        string clientId,
        string clientSecret,
        string? scope,
        CancellationToken ct = default)
    {
        var key = Key(tokenUrl, clientId, scope);
        if (_tokens.TryGetValue(key, out var cached) && cached.ExpiresAt - RefreshSkew > DateTimeOffset.UtcNow)
        {
            return cached.AccessToken;
        }

        await _refreshGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Double-check under the gate.
            if (_tokens.TryGetValue(key, out cached) && cached.ExpiresAt - RefreshSkew > DateTimeOffset.UtcNow)
            {
                return cached.AccessToken;
            }

            var fresh = await FetchAsync(tokenUrl, clientId, clientSecret, scope, ct).ConfigureAwait(false);
            _tokens[key] = fresh;
            return fresh.AccessToken;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    /// <summary>Forces a refresh on the next <see cref="GetTokenAsync"/> call for this key.</summary>
    public void Invalidate(string tokenUrl, string clientId, string? scope) =>
        _tokens.TryRemove(Key(tokenUrl, clientId, scope), out _);

    private async Task<CachedToken> FetchAsync(string tokenUrl, string clientId, string clientSecret, string? scope, CancellationToken ct)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
        };
        if (!string.IsNullOrEmpty(scope)) form["scope"] = scope;

        using var resp = await _http.PostAsync(tokenUrl, new FormUrlEncodedContent(form), ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("OAuth2 token endpoint returned an empty body.");

        var expiresAt = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(body.ExpiresIn > 0 ? body.ExpiresIn : 3600);
        return new CachedToken(body.AccessToken!, expiresAt);
    }

    private static string Key(string tokenUrl, string clientId, string? scope) => $"{tokenUrl}|{clientId}|{scope}";

    private sealed record CachedToken(string AccessToken, DateTimeOffset ExpiresAt);

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
        [JsonPropertyName("token_type")] public string? TokenType { get; set; }
    }
}
