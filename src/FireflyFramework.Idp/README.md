# FireflyFramework.Idp

Identity provider abstraction: login, refresh, logout, introspection, MFA, user / role / scope / session management. Mirrors `fireflyframework-idp`.

## Contract

```csharp
public interface IIdpAdapter
{
    // Authentication
    Task<TokenResponse>      LoginAsync(LoginRequest, CancellationToken);
    Task<TokenResponse>      RefreshAsync(RefreshRequest, CancellationToken);
    Task                     LogoutAsync(LogoutRequest, CancellationToken);
    Task<IntrospectionResponse> IntrospectAsync(string accessToken, CancellationToken);
    Task                     RevokeRefreshTokenAsync(string refreshToken, CancellationToken);

    // User info
    Task<UserInfoResponse>   GetUserInfoAsync(string accessToken, CancellationToken);

    // User management
    Task<CreateUserResponse> CreateUserAsync(CreateUserRequest, CancellationToken);
    Task<UpdateUserResponse> UpdateUserAsync(UpdateUserRequest, CancellationToken);
    Task                     DeleteUserAsync(string userId, CancellationToken);

    // Password
    Task                     ChangePasswordAsync(ChangePasswordRequest, CancellationToken);
    Task                     ResetPasswordAsync(string userId, CancellationToken);

    // MFA
    Task<MfaChallengeResponse> MfaChallengeAsync(string userId, CancellationToken);
    Task<TokenResponse>      MfaVerifyAsync(MfaVerifyRequest, CancellationToken);

    // Sessions
    Task<IReadOnlyList<SessionInfo>> ListSessionsAsync(string userId, CancellationToken);
    Task                     RevokeSessionAsync(string userId, string sessionId, CancellationToken);

    // Roles + scopes
    Task<IReadOnlyList<string>> GetRolesAsync(CancellationToken);
    Task<CreateRolesResponse>   CreateRolesAsync(CreateRolesRequest, CancellationToken);
    Task                        AssignRolesToUserAsync(AssignRolesRequest, CancellationToken);
    Task                        RemoveRolesFromUserAsync(AssignRolesRequest, CancellationToken);
    Task<CreateScopeResponse>   CreateScopeAsync(CreateScopeRequest, CancellationToken);

    // Self-service registration (default delegates to CreateUserAsync)
    Task<CreateUserResponse> RegisterUserAsync(RegisterUserRequest, CancellationToken);
}
```

## Adapters in this repo

| Adapter | Backing |
|---|---|
| `FireflyFramework.Idp.AwsCognito` | AWSSDK.CognitoIdentityProvider — full auth + user CRUD + group/role assignment |
| `FireflyFramework.Idp.AzureAd` | MSAL + Microsoft.Graph (login flow + identity, admin via Graph TODO) |
| `FireflyFramework.Idp.Keycloak` | Direct OIDC + Keycloak admin REST (`KeycloakAdminClient`) — full auth + user/role admin |
| `FireflyFramework.Idp.InternalDb` | Self-contained DB-backed IDP (BCrypt + JWT), full CRUD via `IInternalUserRepository` |

Pick one adapter, register it as the only `IIdpAdapter` in DI, and the rest of the framework reads from a single contract.
