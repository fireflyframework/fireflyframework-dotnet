# FireflyFramework.Idp.Keycloak

Keycloak implementation of `IIdpAdapter`. Mirrors
`org.fireflyframework:firefly-idp-keycloak`.

## Overview

This package adapts Firefly Framework's `IIdpAdapter` port to a
Keycloak realm. Authentication flows go through the realm's standard
OpenID Connect endpoints (`/realms/{realm}/protocol/openid-connect/...`),
while administrative operations — user CRUD, role management, password
reset, session listing — go through Keycloak's admin REST API
(`/admin/realms/{realm}/...`).

The adapter ships in two cooperating types: **`KeycloakIdpAdapter`**,
the public `IIdpAdapter` implementation, and **`KeycloakAdminClient`**,
a thin wrapper around Keycloak's admin REST surface. Authentication
operations work without the admin client; administrative operations
require it. This separation lets you wire only what you need: a
front-end service that just authenticates users does not need admin
credentials configured.

Keycloak is the most feature-complete provider Firefly supports, so the
adapter has the **highest coverage**: only `MfaVerifyAsync` and
`CreateScopeAsync` throw `NotSupportedException`. Both are documented
upstream limits — Keycloak verifies MFA inside the password grant
(supply `LoginRequest.MfaCode`), and client scopes are realm-level
admin-console configuration, not runtime API. Every other operation in
the port is wired.

The Java equivalent is `firefly-idp-keycloak`. The mapping is intentional:
`KeycloakIdpAdapter` corresponds to Java's `KeycloakIdpAdapterImpl` and
`KeycloakAdminClient` corresponds to `KeycloakAPIFactory`. Application
code that has been ported from Java should work unchanged.

## When to use this module

Choose Keycloak when:

- You run an **on-premises** or **self-hosted** identity provider.
- You need fine-grained **role and group** management at runtime.
- You want **direct token introspection** via the OIDC
  `/token/introspect` endpoint (other providers force you to validate
  JWTs locally).
- Your operations team is comfortable running an admin user / service
  account.

Avoid Keycloak when you want a fully managed experience — Cognito,
Entra ID, and Logalty/Auth0-class platforms are the natural fits there.

## Mental model

```
+----------------------------+              +-----------------------------+
|  KeycloakIdpAdapter        |              |  Realm OIDC endpoints       |
|  (IIdpAdapter)             | -- HttpClient --> /protocol/openid-connect |
|                            |              |    /token, /logout,         |
|                            |              |    /token/introspect,       |
|                            |              |    /userinfo                |
+-------------+--------------+              +-----------------------------+
              |
              | optional admin operations
              v
+----------------------------+              +-----------------------------+
|  KeycloakAdminClient       | -- HttpClient --> /admin/realms/{realm}    |
|                            |              |    /users, /roles,          |
|                            |              |    /sessions                |
+----------------------------+              +-----------------------------+
```

A bearer token cached on `KeycloakAdminClient` is reused across calls
and refreshed when its expiry approaches.

## Quick start

```csharp
using FireflyFramework.Idp;
using FireflyFramework.Idp.Keycloak;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<KeycloakOptions>(
    builder.Configuration.GetSection(KeycloakOptions.SectionName));

// Authentication only:
builder.Services.AddHttpClient<KeycloakIdpAdapter>();

// Admin operations: register the admin client.
builder.Services.AddHttpClient<KeycloakAdminClient>();

builder.Services.AddSingleton<IIdpAdapter, KeycloakIdpAdapter>();
```

`appsettings.json`:

```json
{
  "Firefly": {
    "Idp": {
      "Keycloak": {
        "ServerUrl":     "https://kc.example.com",
        "Realm":         "myrealm",
        "ClientId":      "myapp",
        "ClientSecret":  "<confidential client secret>",
        "AdminUsername": "<admin user, optional>",
        "AdminPassword": "<admin password, optional>"
      }
    }
  }
}
```

## Public surface

### Types

| Type | One-line description |
|---|---|
| `KeycloakIdpAdapter` | The `IIdpAdapter` implementation. |
| `KeycloakAdminClient` | Thin wrapper over `/admin/realms/{realm}` for user / role / session operations. |
| `KeycloakOptions` | Bound options class (`Firefly:Idp:Keycloak`). |
| `KeycloakSessionInfo` | Record returned by `KeycloakAdminClient.ListSessionsAsync`. |

### `KeycloakIdpAdapter` operations

| Operation | Endpoint / SDK call |
|---|---|
| `LoginAsync` | `POST /protocol/openid-connect/token` with `grant_type=password`. Adds `totp` form field when `LoginRequest.MfaCode` is supplied. |
| `RefreshAsync` | `POST /protocol/openid-connect/token` with `grant_type=refresh_token`. |
| `LogoutAsync` / `RevokeRefreshTokenAsync` | `POST /protocol/openid-connect/logout`. The two methods share the same impl — Keycloak treats logout as refresh-token revocation. |
| `IntrospectAsync` | `POST /protocol/openid-connect/token/introspect`. Returns the full RFC 7662 shape including `realm_access.roles`. |
| `GetUserInfoAsync` | `GET /protocol/openid-connect/userinfo` with the supplied bearer token. |
| `CreateUserAsync` / `UpdateUserAsync` / `DeleteUserAsync` | Admin REST via `KeycloakAdminClient`; `CreateUserAsync` also assigns roles when `request.Roles` is non-empty. |
| `ChangePasswordAsync` | `PUT /admin/realms/{realm}/users/{id}/reset-password` with `temporary=false`. |
| `ResetPasswordAsync` | Same endpoint with a fresh GUID password and `temporary=true` so the user is forced to set a new one on next login. |
| `MfaChallengeAsync` | Returns a freshly-generated challenge id with method `TOTP`. Verification happens through the password grant; this method exists only for API symmetry. |
| `MfaVerifyAsync` | `NotSupportedException` — supply `LoginRequest.MfaCode` instead. |
| `ListSessionsAsync` / `RevokeSessionAsync` | Admin: `/users/{id}/sessions` and `/sessions/{id}`. |
| `GetRolesAsync` / `CreateRolesAsync` | Admin: `/roles`. |
| `AssignRolesToUserAsync` / `RemoveRolesFromUserAsync` | Admin: `/users/{id}/role-mappings/realm`. |
| `CreateScopeAsync` | `NotSupportedException` — scopes are realm-level admin-console config in Keycloak. |

### `KeycloakAdminClient` operations

The admin client is a public type so it can be reused outside the
adapter (e.g. by a maintenance job that needs `LogoutAllAsync(userId)`,
which the port does not expose).

```csharp
Task<string>                              CreateUserAsync(CreateUserRequest, CancellationToken);
Task                                      UpdateUserAsync(string userId, UpdateUserRequest, CancellationToken);
Task                                      DeleteUserAsync(string userId, CancellationToken);
Task                                      ResetPasswordAsync(string userId, string newPassword, bool temporary, CancellationToken);
Task<IReadOnlyList<string>>               GetRealmRolesAsync(CancellationToken);
Task                                      CreateRealmRoleAsync(string roleName, CancellationToken);
Task                                      AssignRolesAsync(string userId, IEnumerable<string> roles, CancellationToken);
Task                                      RemoveRolesAsync(string userId, IEnumerable<string> roles, CancellationToken);
Task<IReadOnlyList<KeycloakSessionInfo>>  ListSessionsAsync(string userId, CancellationToken);
Task                                      RevokeSessionAsync(string sessionId, CancellationToken);
Task                                      LogoutAllAsync(string userId, CancellationToken);
```

The admin client caches the bearer token and refreshes it 10 seconds
before expiry to avoid race conditions across concurrent calls.

## Configuration

| Option | Type | Default | Effect |
|---|---|---|---|
| `ServerUrl` | `string` | `http://localhost:8080` | Base URL of the Keycloak server. Trailing slashes are stripped. |
| `Realm` | `string` | `master` | Realm to authenticate against. |
| `ClientId` | `string` | `""` | OIDC client id. **Required.** |
| `ClientSecret` | `string?` | `null` | Confidential-client secret. Adds `client_secret` to every form post when set. |
| `AdminUsername` | `string?` | `null` | Admin user. When set, `KeycloakAdminClient` uses the password grant; otherwise it uses `client_credentials`. |
| `AdminPassword` | `string?` | `null` | Admin password. |
| `VerifyTokenSignature` | `bool` | `true` | Reserved for future local-validation hooks. |

```json
{
  "Firefly": {
    "Idp": {
      "Keycloak": {
        "ServerUrl":     "https://kc.example.com",
        "Realm":         "myrealm",
        "ClientId":      "myapp",
        "ClientSecret":  "topsecret",
        "AdminUsername": "kc-admin",
        "AdminPassword": "***"
      }
    }
  }
}
```

When `AdminUsername` / `AdminPassword` are configured the admin client
authenticates using a service-account token; without them, admin
operations on the adapter throw `InvalidOperationException` with a
clear message at first call.

## Common patterns

### Pattern 1: Password grant with optional MFA

```csharp
// First-factor only
var token = await idp.LoginAsync(new LoginRequest("alice", "Sup3r$ecret!"), ct);

// First + second factor in one round-trip (Keycloak's TOTP flow)
var token2 = await idp.LoginAsync(new LoginRequest("alice", "Sup3r$ecret!", MfaCode: "123456"), ct);
```

### Pattern 2: Role-aware user creation

```csharp
await idp.CreateUserAsync(new CreateUserRequest(
    Username:   "bob",
    Email:      "bob@x.com",
    Password:   "Tempor4r1Pass!",
    GivenName:  "Bob",
    FamilyName: "Builder",
    Roles:      new[] { "admin", "auditor" },
    Attributes: null), ct);
```

The adapter calls `KeycloakAdminClient.CreateUserAsync` first, then
loops over `Roles` and assigns each one. If role assignment fails the
user is **already created** — that is the same behaviour as the Java
adapter and matches Keycloak's atomicity guarantees.

### Pattern 3: Force-rotate a user's password

```csharp
// Sets a fresh GUID password and marks it temporary; user is prompted on next login.
await idp.ResetPasswordAsync(userId, ct);
```

### Pattern 4: Session inspection and revocation

```csharp
foreach (var session in await idp.ListSessionsAsync(userId, ct))
{
    Console.WriteLine($"{session.SessionId} from {session.IpAddress} since {session.CreatedAt}");
}

await idp.RevokeSessionAsync(userId, sessionId, ct);
```

### Pattern 5: Token introspection on a protected endpoint

```csharp
var info = await idp.IntrospectAsync(accessToken, ct);
if (!info.Active)
{
    return Results.Unauthorized();
}

if (info.Roles?.Contains("admin") != true)
{
    return Results.Forbid();
}
```

## Pitfalls and gotchas

- **`MfaVerifyAsync` is not supported.** Keycloak verifies TOTP inside
  the password grant. Migrate from a two-step UI to a single login form
  that asks for username + password + TOTP, then call
  `LoginAsync(new LoginRequest(u, p, MfaCode: code))`.
- **`CreateScopeAsync` is not supported.** Realm scopes are admin-console
  configuration. If you genuinely need runtime scope creation, write a
  bespoke admin REST call — but treat that as out of band.
- **Role assignment by name uses an extra GET.** `AssignRolesAsync` looks
  up each role by name to obtain its id, because Keycloak's
  `role-mappings` endpoint requires both. Bulk-assign in batches and
  cache role ids if assignment latency matters.
- **The admin token cache refreshes 10 seconds before expiry.** Concurrent
  calls during refresh are safe because the cached token is still valid;
  the next caller after the boundary triggers a refresh.
- **`HttpClient` lifetime matters.** Use `AddHttpClient<KeycloakIdpAdapter>`
  and `AddHttpClient<KeycloakAdminClient>` so the .NET HTTP factory
  manages connection pooling and rotation. Sharing a single transient
  `HttpClient` across a process leaks sockets.

Bad:
```csharp
// Sharing one HttpClient ignores DNS rotation and connection rotation.
builder.Services.AddSingleton(new HttpClient());
builder.Services.AddSingleton<KeycloakIdpAdapter>();
```

Good:
```csharp
builder.Services.AddHttpClient<KeycloakIdpAdapter>();
builder.Services.AddHttpClient<KeycloakAdminClient>();
```

## Internals (for the curious)

`KeycloakIdpAdapter` is a `sealed class`. It treats `HttpClient` as a
constructor dependency (so `AddHttpClient<T>` "just works") and accepts
an **optional** `KeycloakAdminClient`. When the admin client is null,
`RequireAdmin()` throws `InvalidOperationException` at first
admin-method call. This avoids forcing every consumer to register admin
credentials when only authentication is needed.

The OIDC token responses are parsed with `System.Text.Json` directly
from the response stream, avoiding intermediate string allocation. The
introspection response copies the entire JSON object into the
`Claims` dictionary — this is a deliberate fidelity choice so callers
can read provider-specific claims without re-parsing.

Token forms are built as `Dictionary<string, string>` and serialized
with `FormUrlEncodedContent`. We do not use `string.Format` or string
interpolation for any URL except the realm path itself — `TrimEnd('/')`
is applied to `ServerUrl` so trailing slashes never produce double slashes.

The cost model: every admin call is two HTTP round-trips on the first
call (admin token + actual call), then one round-trip on every
subsequent call until the cached token expires.

## Dependencies

| Reference | Why it's there |
|---|---|
| `FireflyFramework.Idp` | The `IIdpAdapter` port and DTOs. |
| `Microsoft.Extensions.Options` | Bound `KeycloakOptions`. |
| `Microsoft.Extensions.Logging.Abstractions` | `ILogger<KeycloakIdpAdapter>`. |
| `Microsoft.Extensions.Http` (transitive) | `HttpClient` factory integration when consumers call `AddHttpClient<>`. |

## Java mapping

| .NET | Java |
|---|---|
| `KeycloakIdpAdapter` | `KeycloakIdpAdapterImpl` (`fireflyframework-idp-keycloak`) |
| `KeycloakAdminClient` | `KeycloakAPIFactory` |
| `KeycloakOptions` | `KeycloakProperties` |
| `KeycloakSessionInfo` | `KeycloakSessionInfo` |
