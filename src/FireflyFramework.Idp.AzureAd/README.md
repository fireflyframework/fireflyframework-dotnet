# FireflyFramework.Idp.AzureAd

## Overview

`FireflyFramework.Idp.AzureAd` is the **Azure AD / Microsoft Entra ID
adapter** for Firefly's `IIdpAdapter` port. Authentication runs
through MSAL (`Microsoft.Identity.Client`) for token acquisition;
admin operations (CRUD on users, password reset, group assignment,
session revocation) go through Microsoft Graph
(`Microsoft.Graph`) wrapped in a small helper class
`AzureAdGraphAdmin`.

It mirrors `org.fireflyframework:firefly-idp-azure-ad` from the Java
line, where the same split lives between MSAL4J / `OkHttp` and the
Microsoft Graph Java SDK. The .NET surface intentionally exposes the
same behavioural model, including the `NotSupportedException` choices
where the Microsoft identity platform does not provide an equivalent.

## Why a separate module?

Azure AD / Entra ID has *two* APIs that solve overlapping problems:
the OIDC-compliant `/oauth2/v2.0/token` endpoint (great for
authentication, no admin operations) and Microsoft Graph (the only
way to manage users, groups, and policies). Any adapter must
wire both. Putting that wiring in its own assembly keeps:

- The OIDC flows (`LoginAsync`, `IntrospectAsync`) pinned to MSAL,
  which understands the full Microsoft identity platform quirks
  (regional authorities, sovereign clouds, conditional access).
- The admin flows (`CreateUserAsync`, `AssignRolesToUserAsync`)
  pinned to Microsoft Graph, which is where the actual data lives.
- A clean dependency boundary: pulling this assembly into a service
  brings in `Microsoft.Graph` and `MSAL` together; nothing else.

## Mental model

```
                ┌──────────────────────────────────┐
                │       IIdpAdapter (port)         │
                └─────────────┬────────────────────┘
                              │ implemented by
                              ▼
                ┌──────────────────────────────────┐
                │       AzureAdIdpAdapter          │
                └─────────────┬────────────────────┘
                              │ delegates
            ┌─────────────────┼─────────────────┐
            │                 │                 │
            ▼                 │                 ▼
   ┌──────────────────┐       │     ┌──────────────────────┐
   │      MSAL        │       │     │  AzureAdGraphAdmin   │
   │  (auth / tokens) │       │     │ (Microsoft.Graph)    │
   └──────────────────┘       │     └──────────────────────┘
                              │             │
                              │             │
            ┌─────────────────┴──┐    ┌─────┴──────────────┐
            │ login / refresh /  │    │ create / update /  │
            │ introspect (local  │    │ delete user, reset │
            │ JWT decode for     │    │ password, list /   │
            │ introspect)        │    │ assign groups,     │
            │                    │    │ revoke sessions    │
            └────────────────────┘    └────────────────────┘
```

The adapter is the seam for the rest of the framework; the helper is
the seam for Microsoft Graph. Application code typically only sees
`IIdpAdapter` and falls back to `AzureAdGraphAdmin` for advanced
admin scenarios that don't fit the port.

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

The `NotSupportedException` cases are all *deliberate* — Azure AD
simply doesn't expose an equivalent, and the framework's contract is
"either run real code or throw with an actionable message." See the
Pitfalls section for what to do in each case.

## `AzureAdGraphAdmin`

Public Graph helper with the same lifecycle as the adapter (singleton,
backed by an `IConfidentialClientApplication`). Methods:

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

When you hit the limits of `IIdpAdapter` (e.g. you need to set
`accountEnabled` or `assignedLicenses`), inject `AzureAdGraphAdmin`
directly and call Graph through it.

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

| Property         | Required for…                       | Notes                                                       |
|------------------|-------------------------------------|-------------------------------------------------------------|
| `TenantId`       | every operation                     | Your Azure AD directory id                                  |
| `ClientId`       | every operation                     | Application (client) registration id                        |
| `ClientSecret`   | admin operations only               | Secret credential for `IConfidentialClientApplication`       |
| `Authority`      | sovereign clouds                    | Default `login.microsoftonline.com`; override for Gov / China |
| `Scopes`         | login                               | OAuth2 scopes — typically `User.Read` plus any app-defined  |

`ClientSecret` is required for `AzureAdGraphAdmin`; without it, admin
methods throw `InvalidOperationException`. For local development you
can omit it and run authentication-only flows; for production you'll
want a certificate-credential variant — extend the helper if you
need that path.

## Common patterns

### Username/password sign-in

```csharp
public sealed class LoginHandler(IIdpAdapter idp)
{
    public async Task<LoginResult> HandleAsync(LoginCommand cmd, CancellationToken ct)
    {
        try
        {
            var tokens = await idp.LoginAsync(
                new LoginRequest(cmd.Username, cmd.Password, scope: "openid profile User.Read"),
                ct);

            return LoginResult.Ok(tokens.AccessToken, tokens.RefreshToken, tokens.IdToken);
        }
        catch (MsalServiceException ex) when (ex.ErrorCode == "invalid_grant")
        {
            return LoginResult.InvalidCredentials();
        }
    }
}
```

ROPC (resource-owner-password) is the only flow `IIdpAdapter`
exposes for Azure AD because it's the only one that's deterministic
and synchronous. For interactive login, use the standard
`Microsoft.Identity.Web` middleware in your ASP.NET app — Firefly
isn't trying to replace that.

### Provisioning a new user

```csharp
var graph = sp.GetRequiredService<AzureAdGraphAdmin>();
var userId = await graph.CreateUserAsync(new CreateUserRequest
{
    DisplayName       = "Ada Lovelace",
    UserPrincipalName = "ada@firefly.onmicrosoft.com",
    MailNickname      = "ada",
    AccountEnabled    = true,
    Password          = "S0meTemporary!",
    ForceChangePasswordNextSignIn = true,
}, ct);

await graph.AssignToGroupAsync(userId, productSupportGroupId, ct);
```

### Revoking on suspicion of compromise

```csharp
public async Task RevokeAndAlertAsync(string userId, CancellationToken ct)
{
    await idp.RevokeSessionAsync(new RevokeSessionRequest(userId), ct);
    await alerts.SecurityIncidentAsync(userId, "All sessions revoked due to suspicion of compromise.", ct);
}
```

`RevokeSessionAsync` collapses to Graph's
`POST /users/{id}/revokeSignInSessions`, which invalidates every
refresh token on every device. The next time the user authenticates,
they'll need to re-prove they own the account.

## Pitfalls and gotchas

- **Conditional Access policies often block ROPC.** A tenant with
  MFA enforced will reject `LoginAsync`. For interactive flows, use
  the auth-code or device-code grant via `Microsoft.Identity.Web`.
  ROPC is appropriate for system-to-system or daemon scenarios where
  MFA isn't applicable.
- **`IntrospectAsync` is local.** It validates signature + lifetime
  but does *not* check revocation — Azure AD doesn't expose RFC 7662.
  If you need real-time revocation, pair the adapter with a Redis
  denylist updated by a webhook from Azure AD's audit log stream.
- **`GetUserInfoAsync` is `NotSupportedException`.** The Microsoft
  identity platform `userinfo` endpoint returns claims, not a full
  user record. Decode the access token's `oid` claim and call
  `AzureAdGraphAdmin.GetUserAsync(oid, ct)` instead.
- **`ListSessionsAsync` is unreachable.** Graph has no per-user
  session-listing API. If you have Audit Logs access, query
  `auditLogs/signIns?$filter=userId eq 'xxx'`.
- **Group ↔ role mismatch.** Firefly treats Azure AD groups as
  roles. Some teams want app-roles instead — fork this adapter to
  swap `*Group*` calls for `appRoleAssignment` if that's your
  convention.
- **`Authority` defaults to public cloud.** For US Government,
  Germany, or China clouds, override it explicitly:
  `https://login.microsoftonline.us`,
  `https://login.microsoftonline.de`, or
  `https://login.partner.microsoftonline.cn`.
- **`ClientSecret` rotation.** When you rotate the secret in Azure,
  the new value won't take effect until the host app reloads
  configuration. Use `IOptionsMonitor<AzureAdOptions>` and snapshot
  on each invocation to support hot-rotation.

## Internals (for the curious)

- `AzureAdIdpAdapter` keeps a single `IPublicClientApplication`
  reused across calls. Token caching is on by default — MSAL silent
  acquisition resolves from the cache when possible.
- `AzureAdGraphAdmin` instantiates a `GraphServiceClient` per request
  using `ClientSecretCredential`. That's intentional: the credential
  honours `IConfidentialClientApplication`'s built-in retry, but the
  Graph client itself isn't thread-safe across long sessions.
- All the `NotSupportedException` cases carry a message that names
  the specific Azure AD limitation. The framework's convention is
  that operators should never need to read the source to figure out
  why an operation isn't available.

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
