using FireflyFramework.Idp;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;

namespace FireflyFramework.Idp.AzureAd;

public sealed class AzureAdOptions
{
    public const string SectionName = "Firefly:Idp:AzureAd";
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string? ClientSecret { get; set; }
    public string Authority { get; set; } = "https://login.microsoftonline.com";
    public string[] Scopes { get; set; } = new[] { "User.Read" };
}

/// <summary>
/// Entra ID + B2C adapter. Authentication runs via MSAL and admin operations through
/// Microsoft Graph (<see cref="AzureAdGraphAdmin"/>). Mirrors Java <c>AzureAdIdpAdapter</c>.
/// </summary>
public sealed class AzureAdIdpAdapter : IIdpAdapter
{
    private readonly IPublicClientApplication _public;
    private readonly AzureAdOptions _opt;
    private readonly AzureAdGraphAdmin? _admin;

    public AzureAdIdpAdapter(IOptions<AzureAdOptions> options, AzureAdGraphAdmin? admin = null)
    {
        _opt = options.Value;
        _public = PublicClientApplicationBuilder.Create(_opt.ClientId)
            .WithAuthority($"{_opt.Authority}/{_opt.TenantId}")
            .Build();
        _admin = admin;
    }

    public async Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        // Microsoft is deprecating ROPC (username/password) for security reasons:
        // https://aka.ms/msal-ropc-migration. We keep this entry point because the
        // IIdpAdapter contract (shared with the Java side) exposes a username/password
        // login method. New services should prefer the device-code, authorization-code,
        // or client-credentials flow exposed by IConfidentialClientApplication.
#pragma warning disable CS0618
        var result = await _public.AcquireTokenByUsernamePassword(_opt.Scopes, request.Username, request.Password)
            .ExecuteAsync(ct).ConfigureAwait(false);
#pragma warning restore CS0618
        return new TokenResponse(result.AccessToken, null, "Bearer",
            (int)(result.ExpiresOn - DateTimeOffset.UtcNow).TotalSeconds, string.Join(' ', result.Scopes), result.IdToken);
    }

    public Task<TokenResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default) =>
        throw new NotSupportedException("Azure AD refresh is via MSAL silent token cache; use IConfidentialClientApplication.AcquireTokenSilentAsync from your app.");

    public Task LogoutAsync(LogoutRequest request, CancellationToken ct = default) =>
        // Logout in Entra ID = revoke all sign-in sessions for the user. The LogoutRequest
        // carries a refresh token, but we need a userId to call the Graph API. The application
        // should pass the user's `oid` claim in LogoutRequest.RefreshToken (or call the admin
        // RevokeSignInSessionsAsync directly) — we treat the field as a userId here.
        RequireAdmin().RevokeSignInSessionsAsync(request.RefreshToken, ct);

    public Task<IntrospectionResponse> IntrospectAsync(string accessToken, CancellationToken ct = default)
    {
        // Microsoft identity platform does not expose RFC 7662 token introspection. The
        // recommended pattern is local validation via JwtBearer middleware. We do a best-effort
        // unsigned JWT decode so callers can read claims; signature/lifetime validation must
        // happen in the calling pipeline.
        try
        {
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(accessToken);
            var sub = jwt.Claims.FirstOrDefault(c => c.Type == "oid" || c.Type == "sub")?.Value;
            var name = jwt.Claims.FirstOrDefault(c => c.Type == "preferred_username" || c.Type == "name")?.Value;
            var roles = jwt.Claims.Where(c => c.Type == "roles").Select(c => c.Value).ToList();
            var claims = jwt.Claims.ToDictionary(c => c.Type, c => (object?)c.Value);
            var active = jwt.ValidTo > DateTime.UtcNow;
            return Task.FromResult(new IntrospectionResponse(active, name, sub, roles, claims));
        }
        catch
        {
            return Task.FromResult(new IntrospectionResponse(false, null, null, null, null));
        }
    }

    public Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default) =>
        // No direct Graph endpoint; the closest is revokeSignInSessions which invalidates all
        // refresh tokens for the user. The application must pass the user id (oid claim) here.
        RequireAdmin().RevokeSignInSessionsAsync(refreshToken, ct);

    public Task<UserInfoResponse> GetUserInfoAsync(string accessToken, CancellationToken ct = default) =>
        throw new NotSupportedException("Decode the access-token `oid` claim from your app and call AzureAdGraphAdmin.GetUserAsync directly.");

    public async Task<CreateUserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var id = await RequireAdmin().CreateUserAsync(request, ct).ConfigureAwait(false);
        return new CreateUserResponse(id);
    }

    public async Task<UpdateUserResponse> UpdateUserAsync(UpdateUserRequest request, CancellationToken ct = default)
    {
        await RequireAdmin().UpdateUserAsync(request.UserId, request, ct).ConfigureAwait(false);
        return new UpdateUserResponse(request.UserId);
    }

    public Task DeleteUserAsync(string userId, CancellationToken ct = default) =>
        RequireAdmin().DeleteUserAsync(userId, ct);

    public Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default) =>
        RequireAdmin().ResetPasswordAsync(request.UserId, request.NewPassword, ct);

    public Task ResetPasswordAsync(string userId, CancellationToken ct = default) =>
        RequireAdmin().ResetPasswordAsync(userId, Guid.NewGuid().ToString("N"), ct);

    public Task<MfaChallengeResponse> MfaChallengeAsync(string userId, CancellationToken ct = default) =>
        Task.FromResult(new MfaChallengeResponse(Guid.NewGuid().ToString("N"), "MicrosoftAuthenticator"));

    public Task<TokenResponse> MfaVerifyAsync(MfaVerifyRequest request, CancellationToken ct = default) =>
        throw new NotSupportedException("Azure AD MFA flows through the auth-code or device-code flows.");

    public Task<IReadOnlyList<SessionInfo>> ListSessionsAsync(string userId, CancellationToken ct = default) =>
        // Microsoft Graph has no "list sessions" endpoint. The closest is auditLogs/signIns,
        // which is a different access tier (audit log read).
        throw new NotSupportedException("Entra ID exposes no per-user session-listing API. Query auditLogs/signIns if you have audit-log access.");

    public Task RevokeSessionAsync(string userId, string sessionId, CancellationToken ct = default) =>
        // Like the Cognito mapping: we collapse a single-session revoke to revoke-all because
        // Graph has no per-session revoke. sessionId is therefore unused.
        RequireAdmin().RevokeSignInSessionsAsync(userId, ct);

    public Task<IReadOnlyList<string>> GetRolesAsync(CancellationToken ct = default) =>
        RequireAdmin().ListGroupsAsync(ct);

    public async Task<CreateRolesResponse> CreateRolesAsync(CreateRolesRequest request, CancellationToken ct = default)
    {
        var created = new List<string>();
        foreach (var role in request.RoleNames)
        {
            var id = await RequireAdmin().CreateGroupAsync(role, description: null, ct).ConfigureAwait(false);
            created.Add(id);
        }
        return new CreateRolesResponse(created);
    }

    public async Task AssignRolesToUserAsync(AssignRolesRequest request, CancellationToken ct = default)
    {
        var admin = RequireAdmin();
        foreach (var group in request.RoleNames)
        {
            await admin.AssignToGroupAsync(request.UserId, group, ct).ConfigureAwait(false);
        }
    }

    public async Task RemoveRolesFromUserAsync(AssignRolesRequest request, CancellationToken ct = default)
    {
        var admin = RequireAdmin();
        foreach (var group in request.RoleNames)
        {
            await admin.RemoveFromGroupAsync(request.UserId, group, ct).ConfigureAwait(false);
        }
    }

    public Task<CreateScopeResponse> CreateScopeAsync(CreateScopeRequest request, CancellationToken ct = default) =>
        throw new NotSupportedException("Azure AD scopes are configured on app registrations, not at runtime.");

    private AzureAdGraphAdmin RequireAdmin() => _admin
        ?? throw new InvalidOperationException("Azure AD admin operations require AzureAdGraphAdmin — set ClientSecret in AzureAdOptions and register it in DI.");
}
