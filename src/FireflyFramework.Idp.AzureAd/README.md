# FireflyFramework.Idp.AzureAd

Azure AD / Entra ID `IIdpAdapter`. Authentication runs through MSAL
(`Microsoft.Identity.Client`); admin operations go through Microsoft
Graph (`Microsoft.Graph`) in `AzureAdGraphAdmin`.

Mirrors `org.fireflyframework:firefly-idp-azure-ad`.

## Coverage

| Operation                          | Mapping                                                                  |
|------------------------------------|--------------------------------------------------------------------------|
| `LoginAsync`                       | `IPublicClientApplication.AcquireTokenByUsernamePassword`                |
| `RefreshAsync`                     | `NotSupportedException` — refresh runs through the MSAL silent token cache; call `AcquireTokenSilentAsync` from your application |
| `LogoutAsync` / `RevokeRefreshTokenAsync` / `RevokeSessionAsync` | Graph `POST /users/{id}/revokeSignInSessions` (collapses to revoke-all)  |
| `IntrospectAsync`                  | Local JWT decode + lifetime check — Microsoft identity platform has no RFC 7662 endpoint |
| `GetUserInfoAsync`                 | `NotSupportedException` — decode the access token's `oid` claim and call `AzureAdGraphAdmin.GetUserAsync` directly |
| `CreateUserAsync` / `UpdateUserAsync` / `DeleteUserAsync` | Graph `Users.{Post,Patch,Delete}`                  |
| `ChangePasswordAsync` / `ResetPasswordAsync` | `AzureAdGraphAdmin.ResetPasswordAsync`                          |
| `MfaChallengeAsync`                | Returns a placeholder challenge id; verification flows through the auth-code or device-code grants |
| `MfaVerifyAsync` / `CreateScopeAsync` | `NotSupportedException` — flows are handled outside the Idp port    |
| `ListSessionsAsync`                | `NotSupportedException` — Graph has no per-user session-listing API; query `auditLogs/signIns` if you have audit-log access |
| `GetRolesAsync`                    | `AzureAdGraphAdmin.ListGroupsAsync`                                      |
| `CreateRolesAsync`                 | `AzureAdGraphAdmin.CreateGroupAsync`                                     |
| `AssignRolesToUserAsync` / `RemoveRolesFromUserAsync` | `AzureAdGraphAdmin.AssignToGroupAsync` / `RemoveFromGroupAsync` |

## `AzureAdGraphAdmin`

Public Graph helper with the same lifecycle as the adapter. Methods:

```csharp
Task<string>            CreateUserAsync(CreateUserRequest, CancellationToken);
Task                    UpdateUserAsync(string userId, UpdateUserRequest, CancellationToken);
Task                    DeleteUserAsync(string userId, CancellationToken);
Task                    ResetPasswordAsync(string userId, string newPassword, CancellationToken);
Task<UserInfoResponse?> GetUserAsync(string userId, CancellationToken);
Task<IReadOnlyList<string>> ListGroupsAsync(CancellationToken);
Task                    AssignToGroupAsync(string userId, string groupId, CancellationToken);
Task                    RemoveFromGroupAsync(string userId, string groupId, CancellationToken);
Task                    RevokeSignInSessionsAsync(string userId, CancellationToken);
Task<string>            CreateGroupAsync(string displayName, string? description, CancellationToken);
```

## Configuration

```json
{
  "Firefly": {
    "Idp": {
      "AzureAd": {
        "TenantId":     "00000000-0000-0000-0000-000000000000",
        "ClientId":     "00000000-0000-0000-0000-000000000000",
        "ClientSecret": "<set when admin operations are required>",
        "Authority":    "https://login.microsoftonline.com",
        "Scopes":       [ "User.Read" ]
      }
    }
  }
}
```

`ClientSecret` is required for `AzureAdGraphAdmin`; without it, admin
methods throw `InvalidOperationException`.

## Dependencies

| Reference                            | Used for                          |
|--------------------------------------|-----------------------------------|
| `FireflyFramework.Idp`               | `IIdpAdapter`                     |
| `Microsoft.Identity.Client`          | MSAL token acquisition            |
| `Microsoft.Graph`                    | User / group / revoke-sessions    |
| `Azure.Identity`                     | `ClientSecretCredential`          |

## Java mapping

| .NET                  | Java                                       |
|-----------------------|--------------------------------------------|
| `AzureAdIdpAdapter`   | `AzureAdIdpAdapter`                        |
| `AzureAdGraphAdmin`   | `EntraIdAdminService`                      |
| `AzureAdOptions`      | `AzureAdProperties`                        |
