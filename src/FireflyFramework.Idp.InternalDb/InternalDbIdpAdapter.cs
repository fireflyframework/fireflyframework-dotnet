using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FireflyFramework.Idp;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FireflyFramework.Idp.InternalDb;

internal static class ClaimsPrincipalExtensions
{
    public static string? ClaimValue(this ClaimsPrincipal principal, string claimType) =>
        principal.FindFirst(claimType)?.Value;
}

public sealed class InternalDbOptions
{
    public const string SectionName = "Firefly:Idp:InternalDb";
    public string Issuer { get; set; } = "fireflyframework";
    public string Audience { get; set; } = "fireflyframework";
    public string SigningKey { get; set; } = "ChangeMe-ChangeMe-ChangeMe-ChangeMe!"; // 32+ chars
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(30);
}

/// <summary>SPI for storing user records — implement against your preferred DB.</summary>
public interface IInternalUserRepository
{
    Task<InternalUser?> FindByUsernameAsync(string username, CancellationToken ct = default);
    Task<InternalUser?> FindByIdAsync(string userId, CancellationToken ct = default);
    Task<InternalUser> CreateAsync(InternalUser user, CancellationToken ct = default);
    Task UpdateAsync(InternalUser user, CancellationToken ct = default);
    Task DeleteAsync(string userId, CancellationToken ct = default);
}

/// <summary>
/// Stateless JWTs cannot be revoked at the issuer side without a denylist. Implement this
/// against Redis (preferred) or your DB to back logout / revoke-refresh-token.
/// </summary>
public interface ITokenRevocationStore
{
    Task RevokeAsync(string jti, DateTimeOffset until, CancellationToken ct = default);
    Task<bool> IsRevokedAsync(string jti, CancellationToken ct = default);
}

/// <summary>Default in-process implementation. Replace with Redis-backed for clusters.</summary>
public sealed class InMemoryTokenRevocationStore : ITokenRevocationStore
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset> _revoked = new();

    public Task RevokeAsync(string jti, DateTimeOffset until, CancellationToken ct = default)
    {
        _revoked[jti] = until;
        return Task.CompletedTask;
    }

    public Task<bool> IsRevokedAsync(string jti, CancellationToken ct = default)
    {
        if (!_revoked.TryGetValue(jti, out var until)) return Task.FromResult(false);
        if (until < DateTimeOffset.UtcNow)
        {
            _revoked.TryRemove(jti, out _);
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }
}

/// <summary>
/// Catalog of role names. Without it, <c>GetRolesAsync</c> would return whatever roles are
/// observed across users — not a true catalog. Implement against your DB / config source.
/// </summary>
public interface IRoleCatalog
{
    Task<IReadOnlyList<string>> ListAsync(CancellationToken ct = default);
    Task AddAsync(string roleName, CancellationToken ct = default);
    Task RemoveAsync(string roleName, CancellationToken ct = default);
}

public sealed class InMemoryRoleCatalog : IRoleCatalog
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _roles = new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<string>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>(_roles.Keys.OrderBy(k => k).ToList());

    public Task AddAsync(string roleName, CancellationToken ct = default) { _roles[roleName] = 1; return Task.CompletedTask; }
    public Task RemoveAsync(string roleName, CancellationToken ct = default) { _roles.TryRemove(roleName, out _); return Task.CompletedTask; }
}

public sealed class InternalUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }
    public List<string> Roles { get; set; } = new();
    public bool MfaEnabled { get; set; }
}

/// <summary>BCrypt + stateless JWT identity provider. Mirrors Java <c>InternalDbIdpAdapter</c>.</summary>
public sealed class InternalDbIdpAdapter : IIdpAdapter
{
    private readonly IInternalUserRepository _repo;
    private readonly InternalDbOptions _opt;
    private readonly SigningCredentials _creds;
    private readonly ITokenRevocationStore _revocations;
    private readonly IRoleCatalog _roleCatalog;

    public InternalDbIdpAdapter(
        IInternalUserRepository repo,
        IOptions<InternalDbOptions> options,
        ITokenRevocationStore? revocations = null,
        IRoleCatalog? roleCatalog = null)
    {
        _repo = repo;
        _opt = options.Value;
        _creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.SigningKey)), SecurityAlgorithms.HmacSha256);
        _revocations = revocations ?? new InMemoryTokenRevocationStore();
        _roleCatalog = roleCatalog ?? new InMemoryRoleCatalog();
    }

    public async Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _repo.FindByUsernameAsync(request.Username, ct).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException("Invalid credentials");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        return BuildTokens(user);
    }

    public async Task<TokenResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var principal = ValidateToken(request.RefreshToken, validateLifetime: true);
        var jti = principal.ClaimValue(JwtRegisteredClaimNames.Jti);
        if (jti is not null && await _revocations.IsRevokedAsync(jti, ct).ConfigureAwait(false))
        {
            throw new UnauthorizedAccessException("Refresh token has been revoked");
        }

        var sub = principal.ClaimValue(JwtRegisteredClaimNames.Sub) ?? principal.ClaimValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
        var user = await _repo.FindByIdAsync(sub, ct).ConfigureAwait(false) ?? throw new UnauthorizedAccessException();
        return BuildTokens(user);
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken ct = default)
    {
        // Add the refresh token's jti to the denylist so subsequent refreshes fail.
        await RevokeRefreshTokenAsync(request.RefreshToken, ct).ConfigureAwait(false);
    }

    public async Task<IntrospectionResponse> IntrospectAsync(string accessToken, CancellationToken ct = default)
    {
        try
        {
            var principal = ValidateToken(accessToken, validateLifetime: true);
            var jti = principal.ClaimValue(JwtRegisteredClaimNames.Jti);
            if (jti is not null && await _revocations.IsRevokedAsync(jti, ct).ConfigureAwait(false))
            {
                return new IntrospectionResponse(false, null, null, null, null);
            }

            var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            var claims = principal.Claims.ToDictionary(c => c.Type, c => (object?)c.Value);
            return new IntrospectionResponse(true,
                principal.ClaimValue(ClaimTypes.Name),
                principal.ClaimValue(JwtRegisteredClaimNames.Sub) ?? principal.ClaimValue(ClaimTypes.NameIdentifier),
                roles, claims);
        }
        catch (Exception)
        {
            return new IntrospectionResponse(false, null, null, null, null);
        }
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        // Decode without lifetime validation so already-expired tokens can still be marked.
        try
        {
            var principal = ValidateToken(refreshToken, validateLifetime: false);
            var jti = principal.ClaimValue(JwtRegisteredClaimNames.Jti);
            if (jti is not null)
            {
                await _revocations.RevokeAsync(jti, DateTimeOffset.UtcNow.Add(_opt.RefreshTokenLifetime), ct).ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
            // unparseable token — nothing to revoke
        }
    }

    public async Task<UserInfoResponse> GetUserInfoAsync(string accessToken, CancellationToken ct = default)
    {
        var principal = ValidateToken(accessToken, validateLifetime: true);
        var sub = principal.ClaimValue(JwtRegisteredClaimNames.Sub) ?? principal.ClaimValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
        var user = await _repo.FindByIdAsync(sub, ct).ConfigureAwait(false) ?? throw new UnauthorizedAccessException();
        return new UserInfoResponse(user.Id, user.Email, user.GivenName, user.FamilyName, user.Roles, null);
    }

    public async Task<CreateUserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var user = new InternalUser
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password ?? Guid.NewGuid().ToString()),
            GivenName = request.GivenName,
            FamilyName = request.FamilyName,
            Roles = request.Roles?.ToList() ?? new(),
        };

        var created = await _repo.CreateAsync(user, ct).ConfigureAwait(false);
        return new CreateUserResponse(created.Id);
    }

    public async Task<UpdateUserResponse> UpdateUserAsync(UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await _repo.FindByIdAsync(request.UserId, ct).ConfigureAwait(false) ?? throw new UnauthorizedAccessException();
        user.Email = request.Email ?? user.Email;
        user.GivenName = request.GivenName ?? user.GivenName;
        user.FamilyName = request.FamilyName ?? user.FamilyName;
        await _repo.UpdateAsync(user, ct).ConfigureAwait(false);
        return new UpdateUserResponse(user.Id);
    }

    public Task DeleteUserAsync(string userId, CancellationToken ct = default) => _repo.DeleteAsync(userId, ct);

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await _repo.FindByIdAsync(request.UserId, ct).ConfigureAwait(false) ?? throw new UnauthorizedAccessException();
        if (!BCrypt.Net.BCrypt.Verify(request.OldPassword, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Old password mismatch");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _repo.UpdateAsync(user, ct).ConfigureAwait(false);
    }

    public Task ResetPasswordAsync(string userId, CancellationToken ct = default) => Task.CompletedTask;
    public Task<MfaChallengeResponse> MfaChallengeAsync(string userId, CancellationToken ct = default) =>
        Task.FromResult(new MfaChallengeResponse(Guid.NewGuid().ToString("N"), "TOTP"));
    public Task<TokenResponse> MfaVerifyAsync(MfaVerifyRequest request, CancellationToken ct = default) =>
        throw new NotSupportedException("Wire up TOTP in IInternalUserRepository to verify MFA codes.");
    public Task<IReadOnlyList<SessionInfo>> ListSessionsAsync(string userId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SessionInfo>>(Array.Empty<SessionInfo>());
    public Task RevokeSessionAsync(string userId, string sessionId, CancellationToken ct = default) => Task.CompletedTask;
    public Task<IReadOnlyList<string>> GetRolesAsync(CancellationToken ct = default) => _roleCatalog.ListAsync(ct);

    public async Task<CreateRolesResponse> CreateRolesAsync(CreateRolesRequest request, CancellationToken ct = default)
    {
        foreach (var role in request.RoleNames)
        {
            await _roleCatalog.AddAsync(role, ct).ConfigureAwait(false);
        }
        return new CreateRolesResponse(request.RoleNames.ToList());
    }
    public async Task AssignRolesToUserAsync(AssignRolesRequest request, CancellationToken ct = default)
    {
        var user = await _repo.FindByIdAsync(request.UserId, ct).ConfigureAwait(false) ?? throw new UnauthorizedAccessException();
        foreach (var role in request.RoleNames) if (!user.Roles.Contains(role)) user.Roles.Add(role);
        await _repo.UpdateAsync(user, ct).ConfigureAwait(false);
    }

    public async Task RemoveRolesFromUserAsync(AssignRolesRequest request, CancellationToken ct = default)
    {
        var user = await _repo.FindByIdAsync(request.UserId, ct).ConfigureAwait(false) ?? throw new UnauthorizedAccessException();
        foreach (var role in request.RoleNames) user.Roles.Remove(role);
        await _repo.UpdateAsync(user, ct).ConfigureAwait(false);
    }

    public Task<CreateScopeResponse> CreateScopeAsync(CreateScopeRequest request, CancellationToken ct = default) =>
        Task.FromResult(new CreateScopeResponse(Guid.NewGuid().ToString("N")));

    private TokenResponse BuildTokens(InternalUser user)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
        };

        claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var accessToken = new JwtSecurityToken(_opt.Issuer, _opt.Audience, claims,
            now, now.Add(_opt.AccessTokenLifetime), _creds);
        var refreshToken = new JwtSecurityToken(_opt.Issuer, _opt.Audience,
            new[] { new Claim(JwtRegisteredClaimNames.Sub, user.Id) },
            now, now.Add(_opt.RefreshTokenLifetime), _creds);

        var handler = new JwtSecurityTokenHandler();
        return new TokenResponse(handler.WriteToken(accessToken), handler.WriteToken(refreshToken),
            "Bearer", (int)_opt.AccessTokenLifetime.TotalSeconds, null);
    }

    private ClaimsPrincipal ValidateToken(string token, bool validateLifetime)
    {
        var handler = new JwtSecurityTokenHandler();
        return handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidIssuer = _opt.Issuer,
            ValidAudience = _opt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.SigningKey)),
            ValidateIssuerSigningKey = true,
            ValidateLifetime = validateLifetime,
        }, out _);
    }
}
