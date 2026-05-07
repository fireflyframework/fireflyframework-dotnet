using System.Net.Http.Json;
using System.Text.Json;
using FireflyFramework.Idp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FireflyFramework.Idp.Keycloak;

/// <summary>
/// Keycloak <see cref="IIdpAdapter"/> implementation. Talks to the realm's OpenID Connect
/// endpoints for authentication and (optionally) the admin REST API for user/role management.
/// Mirrors Java <c>IdpAdapterImpl</c> + <c>KeycloakClientFactory</c>.
/// </summary>
/// <remarks>
/// When <see cref="KeycloakAdminClient"/> is supplied (it is registered automatically by
/// the DI extension when admin credentials are configured), all admin operations
/// (user CRUD, role mgmt, password reset) are wired. Without it, those operations
/// throw <see cref="NotSupportedException"/> with a clear message.
/// </remarks>
public sealed class KeycloakIdpAdapter : IIdpAdapter
{
    private readonly HttpClient _http;
    private readonly KeycloakOptions _opt;
    private readonly KeycloakAdminClient? _admin;
    private readonly ILogger<KeycloakIdpAdapter> _log;

    public KeycloakIdpAdapter(
        HttpClient http,
        IOptions<KeycloakOptions> options,
        ILogger<KeycloakIdpAdapter> log,
        KeycloakAdminClient? admin = null)
    {
        _http = http;
        _opt = options.Value;
        _log = log;
        _admin = admin;
    }

    private string OidcUrl(string suffix) =>
        $"{_opt.ServerUrl.TrimEnd('/')}/realms/{_opt.Realm}/protocol/openid-connect/{suffix}";

    public async Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = _opt.ClientId,
            ["username"] = request.Username,
            ["password"] = request.Password,
        };

        if (_opt.ClientSecret is not null) form["client_secret"] = _opt.ClientSecret;
        if (request.MfaCode is not null) form["totp"] = request.MfaCode;

        var resp = await _http.PostAsync(OidcUrl("token"), new FormUrlEncodedContent(form), ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return await ReadTokenAsync(resp, ct).ConfigureAwait(false);
    }

    public async Task<TokenResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _opt.ClientId,
            ["refresh_token"] = request.RefreshToken,
        };

        if (_opt.ClientSecret is not null) form["client_secret"] = _opt.ClientSecret;
        var resp = await _http.PostAsync(OidcUrl("token"), new FormUrlEncodedContent(form), ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return await ReadTokenAsync(resp, ct).ConfigureAwait(false);
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = _opt.ClientId,
            ["refresh_token"] = request.RefreshToken,
        };

        if (_opt.ClientSecret is not null) form["client_secret"] = _opt.ClientSecret;
        var resp = await _http.PostAsync(OidcUrl("logout"), new FormUrlEncodedContent(form), ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<IntrospectionResponse> IntrospectAsync(string accessToken, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = _opt.ClientId,
            ["token"] = accessToken,
        };

        if (_opt.ClientSecret is not null) form["client_secret"] = _opt.ClientSecret;
        var resp = await _http.PostAsync(OidcUrl("token/introspect"), new FormUrlEncodedContent(form), ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false), cancellationToken: ct).ConfigureAwait(false);
        var root = doc.RootElement;
        var active = root.TryGetProperty("active", out var a) && a.GetBoolean();
        return new IntrospectionResponse(
            active,
            root.TryGetProperty("username", out var u) ? u.GetString() : null,
            root.TryGetProperty("sub", out var s) ? s.GetString() : null,
            root.TryGetProperty("realm_access", out var r) && r.TryGetProperty("roles", out var roles)
                ? roles.EnumerateArray().Select(x => x.GetString()!).ToList()
                : null,
            root.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value.ToString()));
    }

    public Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default) =>
        LogoutAsync(new LogoutRequest(refreshToken), ct);

    public async Task<UserInfoResponse> GetUserInfoAsync(string accessToken, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, OidcUrl("userinfo"));
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false), cancellationToken: ct).ConfigureAwait(false);
        var root = doc.RootElement;
        return new UserInfoResponse(
            root.GetProperty("sub").GetString()!,
            root.TryGetProperty("email", out var e) ? e.GetString() : null,
            root.TryGetProperty("given_name", out var g) ? g.GetString() : null,
            root.TryGetProperty("family_name", out var f) ? f.GetString() : null,
            null,
            root.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value.ToString()));
    }

    public async Task<CreateUserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var admin = RequireAdmin();
        var userId = await admin.CreateUserAsync(request, ct).ConfigureAwait(false);
        if (request.Roles?.Count > 0)
        {
            await admin.AssignRolesAsync(userId, request.Roles, ct).ConfigureAwait(false);
        }

        return new CreateUserResponse(userId);
    }

    public async Task<UpdateUserResponse> UpdateUserAsync(UpdateUserRequest request, CancellationToken ct = default)
    {
        var admin = RequireAdmin();
        await admin.UpdateUserAsync(request.UserId, request, ct).ConfigureAwait(false);
        return new UpdateUserResponse(request.UserId);
    }

    public Task DeleteUserAsync(string userId, CancellationToken ct = default) =>
        RequireAdmin().DeleteUserAsync(userId, ct);

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default)
    {
        await RequireAdmin().ResetPasswordAsync(request.UserId, request.NewPassword, temporary: false, ct).ConfigureAwait(false);
    }

    public Task ResetPasswordAsync(string userId, CancellationToken ct = default) =>
        RequireAdmin().ResetPasswordAsync(userId, Guid.NewGuid().ToString("N"), temporary: true, ct);

    public Task<MfaChallengeResponse> MfaChallengeAsync(string userId, CancellationToken ct = default) =>
        Task.FromResult(new MfaChallengeResponse(Guid.NewGuid().ToString("N"), "TOTP"));

    public Task<TokenResponse> MfaVerifyAsync(MfaVerifyRequest request, CancellationToken ct = default)
        => throw new NotSupportedException("Keycloak MFA verify is part of the password grant; supply MfaCode in LoginRequest instead.");

    public async Task<IReadOnlyList<SessionInfo>> ListSessionsAsync(string userId, CancellationToken ct = default)
    {
        var sessions = await RequireAdmin().ListSessionsAsync(userId, ct).ConfigureAwait(false);
        return sessions
            .Select(s => new SessionInfo(s.Id, userId, s.Start, s.LastAccess, s.IpAddress, null))
            .ToList();
    }

    public Task RevokeSessionAsync(string userId, string sessionId, CancellationToken ct = default) =>
        RequireAdmin().RevokeSessionAsync(sessionId, ct);

    public Task<IReadOnlyList<string>> GetRolesAsync(CancellationToken ct = default) =>
        RequireAdmin().GetRealmRolesAsync(ct);

    public async Task<CreateRolesResponse> CreateRolesAsync(CreateRolesRequest request, CancellationToken ct = default)
    {
        var admin = RequireAdmin();
        foreach (var role in request.RoleNames)
        {
            await admin.CreateRealmRoleAsync(role, ct).ConfigureAwait(false);
        }

        return new CreateRolesResponse(request.RoleNames.ToList());
    }

    public Task AssignRolesToUserAsync(AssignRolesRequest request, CancellationToken ct = default) =>
        RequireAdmin().AssignRolesAsync(request.UserId, request.RoleNames, ct);

    public Task RemoveRolesFromUserAsync(AssignRolesRequest request, CancellationToken ct = default) =>
        RequireAdmin().RemoveRolesAsync(request.UserId, request.RoleNames, ct);

    public Task<CreateScopeResponse> CreateScopeAsync(CreateScopeRequest request, CancellationToken ct = default)
        => throw new NotSupportedException("Keycloak client scopes must be configured at the realm level via the admin console.");

    private KeycloakAdminClient RequireAdmin() => _admin
        ?? throw new InvalidOperationException("Admin operations require KeycloakAdminClient — configure AdminUsername/AdminPassword in KeycloakOptions and register the admin client in DI.");

    private static async Task<TokenResponse> ReadTokenAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false), cancellationToken: ct).ConfigureAwait(false);
        var root = doc.RootElement;
        return new TokenResponse(
            root.GetProperty("access_token").GetString()!,
            root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null,
            root.TryGetProperty("token_type", out var tt) ? tt.GetString()! : "Bearer",
            root.TryGetProperty("expires_in", out var ex) ? ex.GetInt32() : 0,
            root.TryGetProperty("scope", out var sc) ? sc.GetString() : null,
            root.TryGetProperty("id_token", out var it) ? it.GetString() : null);
    }
}
