# FireflyFramework.Idp.InternalDb

## Overview

`FireflyFramework.Idp.InternalDb` is a **self-hosted `IIdpAdapter`**
for services that need an internal user directory rather than a
third-party identity provider. Passwords are stored as BCrypt hashes;
access and refresh tokens are stateless JWTs signed with HMAC-SHA-256.
Logout works through a pluggable token-revocation store so issued
JWTs can be invalidated before their natural expiry.

It mirrors `org.fireflyframework:firefly-idp-internal-db` from the
Java line. Use it when:

- The service is the system of record for users (B2C portals,
  embedded applications).
- You can't take an external IDP dependency (regulatory, air-gapped,
  cost).
- You need an "always available" fallback for break-glass scenarios.

For most platforms, prefer Keycloak / Cognito / Azure AD — they ship
the auth UX, MFA, social sign-in, and admin console out of the box.
This adapter is the right answer when *you* are the IDP.

## Why a separate module?

The internal IDP carries a different threat model from third-party
adapters:

- **Storage SPI is mandatory.** Unlike Keycloak, where the user store
  is fully external, this adapter persists users itself. Forcing the
  user store to be a pluggable interface keeps the framework free
  of database opinions.
- **Token signing key lives in your config.** That's a sensitive
  surface. Keeping it in a dedicated assembly makes the dependency
  obvious during security review.
- **No social, no MFA out of the box.** This is intentional —
  whoever wires the adapter is taking responsibility for these flows
  themselves. Don't reach for `InternalDb` because it's "simpler";
  reach for it because you've consciously decided to own the IDP.

## Mental model

```
                ┌──────────────────────────────────┐
                │       IIdpAdapter (port)         │
                └─────────────┬────────────────────┘
                              │ implemented by
                              ▼
                ┌──────────────────────────────────┐
                │     InternalDbIdpAdapter         │
                └─────────────┬────────────────────┘
                              │ delegates
            ┌─────────────────┼──────────────────────┐
            │                 │                      │
            ▼                 ▼                      ▼
   ┌──────────────────┐ ┌──────────────────┐ ┌──────────────────────┐
   │ IInternalUser    │ │ ITokenRevocation │ │   IRoleCatalog       │
   │   Repository     │ │     Store        │ │   (role names)       │
   │                  │ │                  │ │                      │
   │ EF Core / Dapper │ │ Redis / EF Core  │ │  EF Core role table  │
   └──────────────────┘ └──────────────────┘ └──────────────────────┘
                              │
                              ▼
                  ┌──────────────────────────┐
                  │   JWT signing (HMAC SHA-256) │
                  │   BCrypt password hashing    │
                  └──────────────────────────────┘
```

The framework supplies the adapter and BCrypt + JWT plumbing. *You*
supply the storage, and that's where most of the operational work
lives.

## Pluggable storage

Application code supplies three SPIs. Default in-memory implementations
are provided for tests; replace them with EF Core / Redis for
production.

| SPI                        | Default implementation             | Replace with                 |
|----------------------------|------------------------------------|------------------------------|
| `IInternalUserRepository`  | (no default — required)            | EF Core / Dapper repository  |
| `ITokenRevocationStore`    | `InMemoryTokenRevocationStore`     | Redis-backed denylist        |
| `IRoleCatalog`             | `InMemoryRoleCatalog`              | EF Core role table           |

```csharp
public interface IInternalUserRepository
{
    Task<InternalUser?> FindByUsernameAsync(string username, CancellationToken ct = default);
    Task<InternalUser?> FindByIdAsync      (string userId,   CancellationToken ct = default);
    Task<InternalUser>  CreateAsync        (InternalUser user, CancellationToken ct = default);
    Task                UpdateAsync        (InternalUser user, CancellationToken ct = default);
    Task                DeleteAsync        (string userId,    CancellationToken ct = default);
}

public interface ITokenRevocationStore
{
    Task RevokeAsync   (string jti, DateTimeOffset until, CancellationToken ct = default);
    Task<bool> IsRevokedAsync(string jti, CancellationToken ct = default);
}

public interface IRoleCatalog
{
    Task<IReadOnlyList<string>> ListAsync (CancellationToken ct = default);
    Task                        AddAsync  (string roleName, CancellationToken ct = default);
    Task                        RemoveAsync(string roleName, CancellationToken ct = default);
}
```

`InternalUser` has `Id`, `Username`, `Email`, `PasswordHash`,
`GivenName`, `FamilyName`, `Roles`, `MfaEnabled`. Treat
`PasswordHash` as opaque — the adapter calls BCrypt internally; your
repository stores whatever string the adapter wrote.

## Coverage

| Operation                                                | Behaviour                                                              |
|----------------------------------------------------------|------------------------------------------------------------------------|
| `LoginAsync`                                             | BCrypt verify + issue access + refresh JWTs                            |
| `RefreshAsync`                                           | Validate refresh token, check revocation store, re-issue tokens        |
| `LogoutAsync` / `RevokeRefreshTokenAsync`                | Add the refresh token's `jti` to the revocation store                  |
| `IntrospectAsync`                                        | Validate signature + lifetime + revocation status                      |
| `GetUserInfoAsync`                                       | Validate access token, look user up by `sub`                           |
| `CreateUserAsync` / `UpdateUserAsync` / `DeleteUserAsync` | Delegated to `IInternalUserRepository`                                |
| `ChangePasswordAsync`                                    | Verify old password (BCrypt) then hash and store new password          |
| `GetRolesAsync` / `CreateRolesAsync`                     | Delegated to `IRoleCatalog`                                            |
| `AssignRolesToUserAsync` / `RemoveRolesFromUserAsync`    | Update the user record's `Roles` collection                            |
| `MfaVerifyAsync`                                         | `NotSupportedException` — wire up TOTP in your `IInternalUserRepository` to verify codes |

## Configuration

```json
{
  "Firefly": {
    "Idp": {
      "InternalDb": {
        "Issuer":                "fireflyframework",
        "Audience":              "fireflyframework",
        "SigningKey":            "<at least 32 ASCII characters>",
        "AccessTokenLifetime":   "01:00:00",
        "RefreshTokenLifetime":  "30.00:00:00"
      }
    }
  }
}
```

| Property                | Default       | Notes                                                 |
|-------------------------|---------------|-------------------------------------------------------|
| `Issuer`                | `fireflyframework` | Set per environment so tokens from staging don't validate in production |
| `Audience`              | `fireflyframework` | Verified on `IntrospectAsync` — set per service mesh    |
| `SigningKey`            | (required)    | At least 32 ASCII characters. Rotate via secrets manager. |
| `AccessTokenLifetime`   | 1 hour        | Short — relying parties refresh frequently            |
| `RefreshTokenLifetime`  | 30 days       | Long — used to obtain a new access token              |

`SigningKey` must be at least 32 characters. The framework refuses
shorter keys at startup — HMAC-SHA-256 needs 256 bits to provide its
nominal security.

## Common patterns

### Wiring with EF Core repositories

```csharp
services.AddDbContext<UserDbContext>(o => o.UseNpgsql(connStr));
services.AddScoped<IInternalUserRepository, EfCoreInternalUserRepository>();
services.AddSingleton<ITokenRevocationStore, RedisTokenRevocationStore>();
services.AddScoped<IRoleCatalog, EfCoreRoleCatalog>();
services.AddInternalDbIdp(configuration);   // wires InternalDbIdpAdapter
```

`ITokenRevocationStore` is a singleton (Redis is shared); the user
repository is scoped (one DbContext per request). Mismatching these
lifetimes is the most common bootstrap mistake.

### Issuing tokens after a custom registration flow

```csharp
public async Task<TokenResponse> RegisterAsync(RegisterCommand cmd, CancellationToken ct)
{
    var user = new InternalUser
    {
        Id           = Guid.NewGuid().ToString(),
        Username     = cmd.Username,
        Email        = cmd.Email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(cmd.Password, workFactor: 12),
        GivenName    = cmd.GivenName,
        FamilyName   = cmd.FamilyName,
        Roles        = new[] { "user" },
        MfaEnabled   = false,
    };
    await users.CreateAsync(user, ct);

    return await idp.LoginAsync(new LoginRequest(cmd.Username, cmd.Password, scope: null), ct);
}
```

### Revoking all tokens for a user

The framework revokes tokens by `jti`. To revoke *all* tokens for a
user (e.g. on password reset), bump a per-user version and embed it
in the JWT — fork the adapter to read/write the version, and reject
introspection if the token's version is older than the user's
current. The default adapter doesn't model this; it's a deliberate
extension point.

### Implementing an EF Core revocation store

```csharp
public sealed class EfCoreTokenRevocationStore(UserDbContext db) : ITokenRevocationStore
{
    public async Task RevokeAsync(string jti, DateTimeOffset until, CancellationToken ct)
    {
        db.RevokedTokens.Add(new RevokedToken { Jti = jti, UntilUtc = until.UtcDateTime });
        await db.SaveChangesAsync(ct);
    }

    public Task<bool> IsRevokedAsync(string jti, CancellationToken ct) =>
        db.RevokedTokens.AnyAsync(r => r.Jti == jti && r.UntilUtc > DateTime.UtcNow, ct);
}
```

A daily cleanup job removes rows older than the maximum token
lifetime so the table stays bounded.

## Pitfalls and gotchas

- **`SigningKey` rotation invalidates every issued token.** Plan for
  it: pre-issue tokens with a `kid` claim, accept multiple keys
  during the rollover window, and remove the old key only after the
  refresh-token lifetime has fully elapsed.
- **BCrypt `workFactor` defaults to 11.** That's fine for 2024
  hardware. Bump to 12-13 if you can spare ~250ms per login. Don't
  drop below 10 for any reason.
- **Stateless JWT means no global revocation.** Without
  `ITokenRevocationStore`, a stolen access token is valid until it
  expires. Configure a Redis-backed revocation store for any
  production deployment.
- **`MfaVerifyAsync` throws `NotSupportedException`.** The adapter
  doesn't ship a TOTP/WebAuthn implementation — you supply one in
  your repository. The simplest path is to store a TOTP secret on
  `InternalUser`, then verify codes during login with
  `Otp.Net` or similar before calling `LoginAsync`.
- **`PasswordHash` is opaque.** Don't try to inspect or compare it
  outside the adapter. BCrypt strings carry the salt and work factor;
  comparing two strings with `==` is meaningless.
- **`IRoleCatalog.RemoveAsync` does not cascade.** Removing a role
  from the catalog doesn't strip it from existing users. Either
  cascade in your repository or accept that historic users may carry
  orphaned role names.
- **Audience matters.** `IntrospectAsync` verifies the `aud` claim.
  If service A issues a token and service B verifies it under a
  different `Audience`, validation fails. Plan your audience strategy
  before deploying.

## Internals (for the curious)

- The adapter uses HMAC-SHA-256 because it's the symmetric default
  for short-lived tokens issued and consumed by the same trust
  domain. For an issuer that needs to be verifiable by external
  parties without sharing the signing secret, fork the adapter to
  use RSA or ECDSA — the SPIs and JWT shape don't change.
- `jti` (JWT ID) is set per token to enable per-token revocation.
  It's a `Guid` in canonical hex form; the revocation store keys on
  it directly.
- `RefreshAsync` re-issues *both* access and refresh tokens — token
  rotation is enabled by default. The previous refresh token is
  added to the revocation store, so refresh-token theft is detected
  the second time the same token is presented.
- `BCrypt.Net-Next` returns a string that includes the algorithm
  identifier, work factor, salt, and hash. The adapter relies on
  this so password verification doesn't need any state beyond the
  hash itself.

## Dependencies

| Reference                            | Used for             |
|--------------------------------------|----------------------|
| `FireflyFramework.Idp`               | `IIdpAdapter`        |
| `BCrypt.Net-Next`                    | Password hashing     |
| `System.IdentityModel.Tokens.Jwt`    | JWT issuance / validation |
| `Microsoft.IdentityModel.Tokens`     | Symmetric keys       |

## Java mapping

| .NET                          | Java                                |
|-------------------------------|-------------------------------------|
| `InternalDbIdpAdapter`        | `InternalDbIdpAdapter`              |
| `IInternalUserRepository`     | `InternalUserRepository`            |
| `ITokenRevocationStore`       | `TokenRevocationStore`              |
| `IRoleCatalog`                | `RoleCatalogService`                |
| `InternalDbOptions`           | `InternalDbProperties`              |
