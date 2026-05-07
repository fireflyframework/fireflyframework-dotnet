# FireflyFramework.Idp.AwsCognito

AWS Cognito `IIdpAdapter`. Authentication runs through
`InitiateAuth` / `RespondToAuthChallenge`; user, group, and session
management goes through the Cognito admin API.

Mirrors `org.fireflyframework:firefly-idp-aws-cognito`.

## Coverage

| Operation                                                  | SDK call                                                     |
|------------------------------------------------------------|--------------------------------------------------------------|
| `LoginAsync`                                               | `InitiateAuthAsync(USER_PASSWORD_AUTH)` with optional `SECRET_HASH` |
| `RefreshAsync`                                             | `InitiateAuthAsync(REFRESH_TOKEN_AUTH)`                      |
| `LogoutAsync`                                              | `GlobalSignOutAsync` for the access token in the request     |
| `IntrospectAsync`                                          | `GetUserAsync` — token is active iff the call succeeds       |
| `RevokeRefreshTokenAsync`                                  | `RevokeTokenAsync`                                           |
| `RevokeSessionAsync`                                       | `AdminUserGlobalSignOutAsync` (sessionId ignored — Cognito has no per-session revoke) |
| `ListSessionsAsync`                                        | `NotSupportedException` — Cognito has no admin session-listing API |
| `GetUserInfoAsync`                                         | `GetUserAsync`                                               |
| `CreateUserAsync` / `UpdateUserAsync` / `DeleteUserAsync`  | Admin user attributes API                                    |
| `ChangePasswordAsync` / `ResetPasswordAsync`               | `AdminSetUserPasswordAsync` / `AdminResetUserPasswordAsync`  |
| `GetRolesAsync` / `CreateRolesAsync`                       | `ListGroupsAsync` / `CreateGroupAsync`                       |
| `AssignRolesToUserAsync` / `RemoveRolesFromUserAsync`      | `AdminAddUserToGroupAsync` / `AdminRemoveUserFromGroupAsync` |
| `MfaChallengeAsync` / `MfaVerifyAsync`                     | `NotSupportedException` — Cognito MFA is part of the InitiateAuth challenge flow |
| `CreateScopeAsync`                                         | `NotSupportedException` — Cognito scopes are configured at the resource server level |

## Configuration

```json
{
  "Firefly": {
    "Idp": {
      "Cognito": {
        "Region":        "eu-west-1",
        "UserPoolId":    "eu-west-1_abcdefghi",
        "ClientId":      "<app client id>",
        "ClientSecret":  "<optional client secret; enables SECRET_HASH>"
      }
    }
  }
}
```

When `ClientSecret` is configured the adapter automatically computes
the `SECRET_HASH` parameter expected by Cognito for confidential clients.

## Dependencies

| Reference                                | Used for                       |
|------------------------------------------|--------------------------------|
| `FireflyFramework.Idp`                   | `IIdpAdapter`                  |
| `AWSSDK.CognitoIdentityProvider`         | Cognito SDK                    |

## Java mapping

| .NET                | Java                  |
|---------------------|-----------------------|
| `CognitoIdpAdapter` | `CognitoIdpAdapter`   |
| `CognitoOptions`    | `CognitoProperties`   |
