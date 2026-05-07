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

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace FireflyFramework.Idp.Keycloak;

/// <summary>
/// Thin wrapper around the Keycloak admin REST API. Acquires admin tokens via the
/// configured admin user / client and exposes user, role and group CRUD calls.
/// Mirrors Java <c>KeycloakAPIFactory</c>.
/// </summary>
public sealed class KeycloakAdminClient
{
    private readonly HttpClient _http;
    private readonly KeycloakOptions _opt;
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiresAt;

    public KeycloakAdminClient(HttpClient http, IOptions<KeycloakOptions> options)
    {
        _http = http;
        _opt = options.Value;
    }

    public async Task<string> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        await AuthenticateAsync(ct).ConfigureAwait(false);
        var payload = new
        {
            username = request.Username,
            email = request.Email,
            firstName = request.GivenName,
            lastName = request.FamilyName,
            enabled = true,
            credentials = request.Password is null
                ? Array.Empty<object>()
                : new object[] { new { type = "password", value = request.Password, temporary = false } },
        };

        using var resp = await _http.PostAsJsonAsync(AdminUrl("users"), payload, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        // Keycloak returns the new user id in the Location header
        var location = resp.Headers.Location?.ToString() ?? throw new InvalidOperationException("Keycloak did not return a Location header");
        return location[(location.LastIndexOf('/') + 1)..];
    }

    public async Task UpdateUserAsync(string userId, UpdateUserRequest request, CancellationToken ct = default)
    {
        await AuthenticateAsync(ct).ConfigureAwait(false);
        var payload = new Dictionary<string, object?>
        {
            ["email"] = request.Email,
            ["firstName"] = request.GivenName,
            ["lastName"] = request.FamilyName,
        };

        using var resp = await _http.PutAsJsonAsync(AdminUrl($"users/{userId}"), payload, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }

    public async Task DeleteUserAsync(string userId, CancellationToken ct = default)
    {
        await AuthenticateAsync(ct).ConfigureAwait(false);
        using var resp = await _http.DeleteAsync(AdminUrl($"users/{userId}"), ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }

    public async Task ResetPasswordAsync(string userId, string newPassword, bool temporary, CancellationToken ct = default)
    {
        await AuthenticateAsync(ct).ConfigureAwait(false);
        var payload = new { type = "password", value = newPassword, temporary };
        using var resp = await _http.PutAsJsonAsync(AdminUrl($"users/{userId}/reset-password"), payload, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<string>> GetRealmRolesAsync(CancellationToken ct = default)
    {
        await AuthenticateAsync(ct).ConfigureAwait(false);
        using var resp = await _http.GetAsync(AdminUrl("roles"), ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false), cancellationToken: ct).ConfigureAwait(false);
        return doc.RootElement.EnumerateArray()
            .Select(r => r.GetProperty("name").GetString()!)
            .ToList();
    }

    public async Task CreateRealmRoleAsync(string roleName, CancellationToken ct = default)
    {
        await AuthenticateAsync(ct).ConfigureAwait(false);
        using var resp = await _http.PostAsJsonAsync(AdminUrl("roles"), new { name = roleName }, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }

    public async Task AssignRolesAsync(string userId, IEnumerable<string> roles, CancellationToken ct = default)
    {
        await AuthenticateAsync(ct).ConfigureAwait(false);
        var roleObjects = new List<object>();
        foreach (var role in roles)
        {
            using var lookup = await _http.GetAsync(AdminUrl($"roles/{role}"), ct).ConfigureAwait(false);
            lookup.EnsureSuccessStatusCode();
            var def = await lookup.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct).ConfigureAwait(false);
            roleObjects.Add(new
            {
                id = def.GetProperty("id").GetString(),
                name = def.GetProperty("name").GetString(),
            });
        }

        using var resp = await _http.PostAsJsonAsync(AdminUrl($"users/{userId}/role-mappings/realm"), roleObjects, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }

    public async Task RemoveRolesAsync(string userId, IEnumerable<string> roles, CancellationToken ct = default)
    {
        await AuthenticateAsync(ct).ConfigureAwait(false);
        var roleObjects = roles.Select(r => new { name = r }).ToList();
        using var req = new HttpRequestMessage(HttpMethod.Delete, AdminUrl($"users/{userId}/role-mappings/realm"))
        {
            Content = JsonContent.Create(roleObjects),
        };

        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }

    /// <summary>Returns the list of active sessions for the user.</summary>
    public async Task<IReadOnlyList<KeycloakSessionInfo>> ListSessionsAsync(string userId, CancellationToken ct = default)
    {
        await AuthenticateAsync(ct).ConfigureAwait(false);
        using var resp = await _http.GetAsync(AdminUrl($"users/{userId}/sessions"), ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false), cancellationToken: ct).ConfigureAwait(false);

        return doc.RootElement.EnumerateArray()
            .Select(e => new KeycloakSessionInfo(
                e.GetProperty("id").GetString()!,
                e.TryGetProperty("ipAddress", out var ip) ? ip.GetString() : null,
                e.TryGetProperty("start", out var s) ? DateTimeOffset.FromUnixTimeMilliseconds(s.GetInt64()) : default,
                e.TryGetProperty("lastAccess", out var la) ? DateTimeOffset.FromUnixTimeMilliseconds(la.GetInt64()) : default))
            .ToList();
    }

    /// <summary>Logs out a single user session.</summary>
    public async Task RevokeSessionAsync(string sessionId, CancellationToken ct = default)
    {
        await AuthenticateAsync(ct).ConfigureAwait(false);
        using var resp = await _http.DeleteAsync(AdminUrl($"sessions/{sessionId}"), ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }

    /// <summary>Logs out *all* sessions for the user.</summary>
    public async Task LogoutAllAsync(string userId, CancellationToken ct = default)
    {
        await AuthenticateAsync(ct).ConfigureAwait(false);
        using var resp = await _http.PostAsync(AdminUrl($"users/{userId}/logout"), content: null, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }

    private async Task AuthenticateAsync(CancellationToken ct)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt - TimeSpan.FromSeconds(10))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _cachedToken);
            return;
        }

        var form = new Dictionary<string, string>
        {
            ["client_id"] = _opt.ClientId,
            ["grant_type"] = _opt.AdminUsername is null ? "client_credentials" : "password",
        };

        if (_opt.ClientSecret is not null) form["client_secret"] = _opt.ClientSecret;
        if (_opt.AdminUsername is not null)
        {
            form["username"] = _opt.AdminUsername;
            form["password"] = _opt.AdminPassword ?? string.Empty;
        }

        var url = $"{_opt.ServerUrl.TrimEnd('/')}/realms/{_opt.Realm}/protocol/openid-connect/token";
        using var resp = await _http.PostAsync(url, new FormUrlEncodedContent(form), ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false), cancellationToken: ct).ConfigureAwait(false);
        _cachedToken = doc.RootElement.GetProperty("access_token").GetString();
        _tokenExpiresAt = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(doc.RootElement.GetProperty("expires_in").GetInt32());
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _cachedToken);
    }

    private string AdminUrl(string suffix) =>
        $"{_opt.ServerUrl.TrimEnd('/')}/admin/realms/{_opt.Realm}/{suffix}";
}

public sealed record KeycloakSessionInfo(string Id, string? IpAddress, DateTimeOffset Start, DateTimeOffset LastAccess);
