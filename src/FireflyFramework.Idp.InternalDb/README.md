# FireflyFramework.Idp.InternalDb

Self-hosted `IIdpAdapter` for services that need an internal user
directory. Passwords are stored as BCrypt hashes; access and refresh
tokens are stateless JWTs signed with HMAC-SHA-256. Logout works
through a pluggable token-revocation store so JWTs can be invalidated
before their natural expiry.

Mirrors `org.fireflyframework:firefly-idp-internal-db`.

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
`GivenName`, `FamilyName`, `Roles`, `MfaEnabled`.

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

`SigningKey` must be at least 32 characters. Rotate it through your
secrets manager.

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
