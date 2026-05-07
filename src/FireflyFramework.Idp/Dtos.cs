namespace FireflyFramework.Idp;

public sealed record LoginRequest(string Username, string Password, string? MfaCode = null);
public sealed record TokenResponse(string AccessToken, string? RefreshToken, string TokenType, int ExpiresIn, string? Scope = null, string? IdToken = null);
public sealed record RefreshRequest(string RefreshToken);
public sealed record LogoutRequest(string RefreshToken);
public sealed record IntrospectionResponse(bool Active, string? Username, string? Sub, IReadOnlyList<string>? Roles, IReadOnlyDictionary<string, object?>? Claims);
public sealed record UserInfoResponse(string Sub, string? Email, string? GivenName, string? FamilyName, IReadOnlyList<string>? Roles, IReadOnlyDictionary<string, object?>? Claims);

public sealed record CreateUserRequest(string Username, string Email, string? Password, string? GivenName, string? FamilyName, IReadOnlyList<string>? Roles, IReadOnlyDictionary<string, object?>? Attributes);
public sealed record CreateUserResponse(string UserId);
public sealed record UpdateUserRequest(string UserId, string? Email, string? GivenName, string? FamilyName, IReadOnlyDictionary<string, object?>? Attributes);
public sealed record UpdateUserResponse(string UserId);

public sealed record ChangePasswordRequest(string UserId, string OldPassword, string NewPassword);
public sealed record CreateRolesRequest(IReadOnlyList<string> RoleNames);
public sealed record CreateRolesResponse(IReadOnlyList<string> CreatedRoleIds);
public sealed record AssignRolesRequest(string UserId, IReadOnlyList<string> RoleNames);

public sealed record CreateScopeRequest(string Name, string? Description);
public sealed record CreateScopeResponse(string ScopeId);

public sealed record MfaChallengeResponse(string ChallengeId, string Method);
public sealed record MfaVerifyRequest(string ChallengeId, string Code);

public sealed record SessionInfo(string SessionId, string UserId, DateTimeOffset CreatedAt, DateTimeOffset? LastActivity, string? IpAddress, string? UserAgent);
public sealed record RegisterUserRequest(string Username, string Email, string Password, string? GivenName, string? FamilyName);
