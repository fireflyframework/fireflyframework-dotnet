# FireflyFramework.Idp.AwsCognito

AWS Cognito implementation of `IIdpAdapter`. Mirrors
`org.fireflyframework:firefly-idp-aws-cognito`.

## Overview

`FireflyFramework.Idp.AwsCognito` adapts Firefly's `IIdpAdapter` port to
Amazon Cognito User Pools using the official `IAmazonCognitoIdentityProvider`
client from `AWSSDK.CognitoIdentityProvider`. It covers the full
authentication lifecycle, plus admin user CRUD and group / role
management. The adapter dispatches `InitiateAuthAsync` for the
`USER_PASSWORD_AUTH` and `REFRESH_TOKEN_AUTH` flows and uses the
`Admin*` family of API calls for everything that requires an admin
context.

Cognito is an OAuth2-with-extensions service rather than a strict OIDC
provider. That has two practical consequences for this adapter. First,
there is no RFC 7662 introspection endpoint — we synthesize one by
calling `GetUserAsync` with the access token and treating
`NotAuthorizedException` as "inactive token". Second, Cognito's MFA and
session lifecycles do not fit the port shape: MFA challenges live
inside the `RespondToAuthChallengeAsync` handshake, and there is no
admin "list sessions" call. Both surfaces throw `NotSupportedException`
with a documented remediation.

The adapter handles the `SECRET_HASH` parameter required for
confidential clients automatically: when `CognitoOptions.ClientSecret`
is set, every `InitiateAuthAsync` call computes and adds the
HMAC-SHA-256 hash of `username + clientId` keyed with the client
secret. Public clients work transparently — leave `ClientSecret`
unconfigured and the parameter is omitted.

The Java equivalent is `firefly-idp-aws-cognito`. The mapping is direct:
`CognitoIdpAdapter` corresponds to Java's `CognitoIdpAdapter`,
`CognitoOptions` corresponds to `CognitoProperties`.

## When to use this module

Choose Cognito when:

- You run on **AWS** and want a **managed** identity service that fits
  inside your existing IAM and VPC perimeter.
- You need built-in **federated identity** (SAML, OIDC, Google, Apple).
- You want **Cognito User Pools** as the source of truth, with groups
  acting as roles.
- You are happy with Cognito's MFA model (challenge-response inside
  `InitiateAuth` / `RespondToAuthChallenge`) — meaning the adapter's
  `MfaVerifyAsync` will not be your code path.

Avoid Cognito when you need realm-style multi-tenancy with deep admin
scripting, or when your operations team does not want a hard AWS
dependency.

## Mental model

```
+-----------------------------+         +-----------------------------+
|  CognitoIdpAdapter          |  uses   |  IAmazonCognitoIdentityProvider |
|  (IIdpAdapter)              | ------> |  (AWSSDK.CognitoIdentityProvider) |
+--------------+--------------+         +--------------+--------------+
               |                                       |
               | LoginAsync, RefreshAsync,             | InitiateAuthAsync,
               | IntrospectAsync, ...                  | GetUserAsync,
               |                                       | RevokeTokenAsync,
               v                                       | AdminCreateUserAsync,
+-----------------------------+                        | AdminUpdateUserAttributesAsync,
|  Cognito User Pool          | <----------------------+ AdminUserGlobalSignOutAsync, ...
+-----------------------------+
```

`SECRET_HASH` is computed inline when needed — there is no second SDK
call to obtain it.

## Quick start

```csharp
using FireflyFramework.Idp;
using FireflyFramework.Idp.AwsCognito;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<CognitoOptions>(
    builder.Configuration.GetSection(CognitoOptions.SectionName));

// CognitoIdpAdapter has two constructors. The (IOptions) overload builds the
// SDK client itself from the configured Region. Pick this for production.
builder.Services.AddSingleton<IIdpAdapter, CognitoIdpAdapter>();
```

If you already have a custom `IAmazonCognitoIdentityProvider` (for
example because you want to share credentials providers or configure
retry policies):

```csharp
builder.Services.AddSingleton<IAmazonCognitoIdentityProvider>(/* your client */);
builder.Services.AddSingleton<IIdpAdapter>(sp =>
    new CognitoIdpAdapter(
        sp.GetRequiredService<IOptions<CognitoOptions>>(),
        sp.GetRequiredService<IAmazonCognitoIdentityProvider>()));
```

`appsettings.json`:

```json
{
  "Firefly": {
    "Idp": {
      "Cognito": {
        "Region":       "eu-west-1",
        "UserPoolId":   "eu-west-1_abcdefghi",
        "ClientId":     "<app client id>",
        "ClientSecret": "<optional, enables SECRET_HASH for confidential clients>"
      }
    }
  }
}
```

## Public surface

### Types

| Type | One-line description |
|---|---|
| `CognitoIdpAdapter` | The `IIdpAdapter` implementation. |
| `CognitoOptions` | Bound options class (`Firefly:Idp:Cognito`). |

### Constructors

```csharp
// Production: builds an AmazonCognitoIdentityProviderClient for the configured Region.
public CognitoIdpAdapter(IOptions<CognitoOptions> options);

// Testing / advanced DI: accepts an explicit IAmazonCognitoIdentityProvider.
public CognitoIdpAdapter(IOptions<CognitoOptions> options, IAmazonCognitoIdentityProvider client);
```

The two-argument form is used by tests (`NSubstitute.For<>`) and by
hosts that want to share a single SDK client across adapters or wire
custom credentials providers / retry policies. Both arguments are
null-checked.

### Operation coverage

| Operation | SDK call |
|---|---|
| `LoginAsync` | `InitiateAuthAsync(USER_PASSWORD_AUTH)` with `USERNAME` / `PASSWORD` (and `SECRET_HASH` when configured). |
| `RefreshAsync` | `InitiateAuthAsync(REFRESH_TOKEN_AUTH)` with `REFRESH_TOKEN`. The response sometimes omits a fresh refresh token; the adapter falls back to the supplied one to keep callers' bookkeeping simple. |
| `LogoutAsync` | `GlobalSignOutAsync` for the access token (the `LogoutRequest.RefreshToken` field is treated as the access token here — the port is access-token-agnostic). |
| `IntrospectAsync` | `GetUserAsync` with the access token. Success → `Active = true`; `NotAuthorizedException` → `Active = false`. |
| `RevokeRefreshTokenAsync` | `RevokeTokenAsync` with the configured client secret (empty string when public client). |
| `GetUserInfoAsync` | `GetUserAsync` — every Cognito attribute is returned in the `Claims` dictionary. |
| `CreateUserAsync` | `AdminCreateUserAsync` with `email`, `given_name`, `family_name` attributes and the password as `TemporaryPassword`. |
| `UpdateUserAsync` | `AdminUpdateUserAttributesAsync` — only the supplied non-null fields are sent. |
| `DeleteUserAsync` | `AdminDeleteUserAsync`. |
| `ChangePasswordAsync` | `AdminSetUserPasswordAsync` with `Permanent = true` (the port has no access-token form, so we use the admin path). |
| `ResetPasswordAsync` | `AdminResetUserPasswordAsync` — Cognito sends the standard reset email. |
| `MfaChallengeAsync` | `NotSupportedException` — Cognito MFA is part of `InitiateAuth`'s challenge response. |
| `MfaVerifyAsync` | `NotSupportedException` — call `RespondToAuthChallengeAsync` directly. |
| `ListSessionsAsync` | `NotSupportedException` — Cognito has no admin session-listing API. The closest is `AdminListDevicesAsync` (remembered devices, not active sessions). |
| `RevokeSessionAsync` | `AdminUserGlobalSignOutAsync` — invalidates **all** sessions for the user; the supplied `sessionId` is ignored. |
| `GetRolesAsync` | `ListGroupsAsync` (Cognito groups model roles) with `Limit = 60`. |
| `CreateRolesAsync` | `CreateGroupAsync` per role. |
| `AssignRolesToUserAsync` | `AdminAddUserToGroupAsync` per role. |
| `RemoveRolesFromUserAsync` | `AdminRemoveUserFromGroupAsync` per role. |
| `CreateScopeAsync` | `NotSupportedException` — Cognito scopes belong to a resource server, configured outside runtime. |

## Configuration

| Option | Type | Default | Effect |
|---|---|---|---|
| `Region` | `string` | `us-east-1` | AWS region for the user pool. Used when constructing the default SDK client. |
| `UserPoolId` | `string` | `""` | Cognito user pool id. **Required for admin operations.** |
| `ClientId` | `string` | `""` | App client id. **Required.** |
| `ClientSecret` | `string?` | `null` | When set, every `InitiateAuthAsync` adds a `SECRET_HASH` parameter computed as `Base64(HMAC-SHA256(clientSecret, username + clientId))`. |

```json
{
  "Firefly": {
    "Idp": {
      "Cognito": {
        "Region":       "eu-west-1",
        "UserPoolId":   "eu-west-1_abcdefghi",
        "ClientId":     "5j4s...c0",
        "ClientSecret": "1u2y...vq"
      }
    }
  }
}
```

## Common patterns

### Pattern 1: Public-client login

```csharp
// CognitoOptions.ClientSecret is null — adapter omits SECRET_HASH automatically.
var token = await idp.LoginAsync(new LoginRequest("alice", "Sup3r$ecret!"), ct);
```

### Pattern 2: Confidential-client login

```csharp
// CognitoOptions.ClientSecret is set — the adapter computes the SECRET_HASH from
// HMAC-SHA-256(secret, username + clientId) on every call.
var token = await idp.LoginAsync(new LoginRequest("alice", "Sup3r$ecret!"), ct);
```

### Pattern 3: Group-based RBAC

Cognito's groups stand in for roles. The adapter exposes them as such.

```csharp
await idp.CreateRolesAsync(new CreateRolesRequest(new[] { "admins", "users" }), ct);
await idp.AssignRolesToUserAsync(new AssignRolesRequest("alice", new[] { "admins" }), ct);

var allRoles = await idp.GetRolesAsync(ct); // ListGroupsAsync(Limit=60)
```

### Pattern 4: "Sign out everywhere"

The port models per-session revocation; Cognito only models global
sign-out. The adapter collapses the operation:

```csharp
// sessionId is ignored — Cognito invalidates every session for the user.
await idp.RevokeSessionAsync("alice", "any-id", ct);
```

If you really want per-session revocation, you cannot get there with
Cognito today.

### Pattern 5: Token introspection without RFC 7662

```csharp
// IntrospectAsync calls GetUserAsync; success means the token is active.
var info = await idp.IntrospectAsync(accessToken, ct);
if (!info.Active)
{
    return Results.Unauthorized();
}
```

## Pitfalls and gotchas

- **`MfaVerifyAsync` and `MfaChallengeAsync` throw.** Cognito's MFA is
  part of the `InitiateAuthAsync` / `RespondToAuthChallengeAsync`
  challenge dance. Call those directly when you need MFA.
- **`ListSessionsAsync` throws.** Cognito has no admin session-listing
  API. If you need device introspection, call `AdminListDevicesAsync`
  on the SDK directly — but devices are not sessions.
- **`RevokeSessionAsync` is global.** Cognito has no per-session API; the
  adapter collapses to `AdminUserGlobalSignOutAsync`.
- **`ChangePasswordAsync` uses the admin path.** The port carries only a
  `userId`, not an access token, so the adapter sets the password with
  `AdminSetUserPasswordAsync(Permanent=true)`. Self-service
  `ChangePasswordAsync` against an access token is not modelled here.
- **`RefreshAsync` returns the supplied refresh token if the response
  omits one.** Cognito sometimes echoes the same refresh token, sometimes
  rotates it. The adapter follows the SDK: `r.RefreshToken ?? request.RefreshToken`.
- **`LogoutAsync.RefreshToken` is actually used as an access token.** The
  port name is provider-neutral; for Cognito, `GlobalSignOutAsync`
  expects an access token. Pass the access token in that field.

Bad:
```csharp
// SECRET_HASH is computed wrong if you supply your own
authParams["SECRET_HASH"] = mySecretHash;
```

Good:
```csharp
// Let the adapter compute the hash from CognitoOptions.ClientSecret
await idp.LoginAsync(new LoginRequest("alice", "..."), ct);
```

## Internals (for the curious)

The adapter is a `sealed class` with two constructors. The single-argument
overload constructs `AmazonCognitoIdentityProviderClient` from the
configured region — that is the path most production wire-ups take.
The two-argument overload accepts an explicit
`IAmazonCognitoIdentityProvider` so tests can substitute a mocked client
(see `tests/FireflyFramework.Tests/CognitoIdpAdapterTests.cs`).

`SECRET_HASH` is computed with `System.Security.Cryptography.HMACSHA256`
inside `LoginAsync` only — `RefreshAsync` does not need it because
Cognito does not validate the secret hash on the refresh-token grant.
The hash itself is `Base64(HMAC(clientSecret, username + clientId))`,
which matches AWS' published formula.

`IntrospectAsync` is the most subtle method: rather than rely on
introspection (which Cognito does not expose), it calls `GetUserAsync`
with the access token. If the token is invalid the SDK throws
`NotAuthorizedException` and the adapter returns
`new IntrospectionResponse(false, null, null, null, null)`. Any other
exception bubbles up — the adapter does not swallow non-auth errors.

The cost model: each operation is one SDK call; admin operations like
`AssignRolesToUserAsync` are N SDK calls (one per role).

## Dependencies

| Reference | Why it's there |
|---|---|
| `FireflyFramework.Idp` | The `IIdpAdapter` port and DTOs. |
| `Microsoft.Extensions.Options` | Bound `CognitoOptions`. |
| `Microsoft.Extensions.Options.ConfigurationExtensions` | `Configure<>().GetSection()` helpers. |
| `AWSSDK.CognitoIdentityProvider` | The Cognito SDK (`IAmazonCognitoIdentityProvider`). |

## Java mapping

| .NET | Java |
|---|---|
| `CognitoIdpAdapter` | `CognitoIdpAdapter` |
| `CognitoOptions` | `CognitoProperties` |
