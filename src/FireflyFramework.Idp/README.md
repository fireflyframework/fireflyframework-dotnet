# FireflyFramework.Idp

Identity-provider abstraction. Defines the `IIdpAdapter` port that
covers authentication, user / role / session management, MFA, and
introspection so application code can stay portable across providers.

Mirrors `org.fireflyframework:firefly-idp`.

## `IIdpAdapter`

```csharp
public interface IIdpAdapter
{
    // Authentication
    Task<TokenResponse>            LoginAsync             (LoginRequest, CancellationToken);
    Task<TokenResponse>            RefreshAsync           (RefreshRequest, CancellationToken);
    Task                           LogoutAsync            (LogoutRequest, CancellationToken);
    Task<IntrospectionResponse>    IntrospectAsync        (string accessToken, CancellationToken);
    Task                           RevokeRefreshTokenAsync(string refreshToken, CancellationToken);

    // Identity
    Task<UserInfoResponse>         GetUserInfoAsync       (string accessToken, CancellationToken);

    // User management
    Task<CreateUserResponse>       CreateUserAsync        (CreateUserRequest, CancellationToken);
    Task<UpdateUserResponse>       UpdateUserAsync        (UpdateUserRequest, CancellationToken);
    Task                           DeleteUserAsync        (string userId, CancellationToken);

    // Password
    Task                           ChangePasswordAsync    (ChangePasswordRequest, CancellationToken);
    Task                           ResetPasswordAsync     (string userId, CancellationToken);

    // MFA
    Task<MfaChallengeResponse>     MfaChallengeAsync      (string userId, CancellationToken);
    Task<TokenResponse>            MfaVerifyAsync         (MfaVerifyRequest, CancellationToken);

    // Sessions
    Task<IReadOnlyList<SessionInfo>> ListSessionsAsync   (string userId, CancellationToken);
    Task                             RevokeSessionAsync  (string userId, string sessionId, CancellationToken);

    // Roles & scopes
    Task<IReadOnlyList<string>>    GetRolesAsync          (CancellationToken);
    Task<CreateRolesResponse>      CreateRolesAsync       (CreateRolesRequest, CancellationToken);
    Task                           AssignRolesToUserAsync (AssignRolesRequest, CancellationToken);
    Task                           RemoveRolesFromUserAsync(AssignRolesRequest, CancellationToken);
    Task<CreateScopeResponse>      CreateScopeAsync       (CreateScopeRequest, CancellationToken);
}
```

Where a particular provider's API does not support an operation (for
example, Microsoft identity platform has no per-user session-listing
endpoint), the adapter throws `NotSupportedException` with a message
documenting the workaround. There are no silent no-ops.

## Adapters in this repository

| Adapter                           | Backing                                  | Coverage                                                                 |
|-----------------------------------|------------------------------------------|--------------------------------------------------------------------------|
| `FireflyFramework.Idp.Keycloak`   | OIDC + Keycloak admin REST API          | Full auth + user / role / session admin via `KeycloakAdminClient`        |
| `FireflyFramework.Idp.AzureAd`    | MSAL + Microsoft.Graph                   | Auth + Graph-based admin (`AzureAdGraphAdmin`); revokeSignInSessions for logout / revoke |
| `FireflyFramework.Idp.AwsCognito` | AWSSDK.CognitoIdentityProvider           | Auth + admin user / group surface; AdminUserGlobalSignOut for revoke     |
| `FireflyFramework.Idp.InternalDb` | EF Core + BCrypt + stateless JWT         | Self-contained; pluggable `IInternalUserRepository`, `ITokenRevocationStore`, `IRoleCatalog` |

Pick one adapter, register it as the only `IIdpAdapter` in DI, and the
rest of the framework reads from a single contract.

## DTOs

`Idp/Dtos.cs` declares every request/response record:
`LoginRequest`, `RefreshRequest`, `LogoutRequest`, `TokenResponse`,
`IntrospectionResponse`, `UserInfoResponse`, `CreateUserRequest` /
`Response`, `UpdateUserRequest` / `Response`, `ChangePasswordRequest`,
`MfaChallengeResponse`, `MfaVerifyRequest`, `SessionInfo`,
`AssignRolesRequest`, `CreateRolesRequest` / `Response`,
`CreateScopeRequest` / `Response`.

## Dependencies

| Reference                  | Used for                |
|----------------------------|-------------------------|
| `FireflyFramework.Kernel`  | Base exceptions         |

## Java mapping

| .NET            | Java          |
|-----------------|---------------|
| `IIdpAdapter`   | `IdpAdapter`  |
| All DTOs        | matching DTO names without the trailing `Dto` suffix |
