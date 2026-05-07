# FireflyFramework.Idp.Keycloak

Keycloak `IIdpAdapter` implementation. Talks to the realm's OpenID
Connect endpoints for token flows and the admin REST API for user /
role / session management.

Mirrors `org.fireflyframework:firefly-idp-keycloak`.

## Coverage

| Operation                                       | Endpoint / SDK call                                                |
|-------------------------------------------------|--------------------------------------------------------------------|
| `LoginAsync` / `RefreshAsync` / `LogoutAsync`   | `POST /realms/{realm}/protocol/openid-connect/{token,logout}`      |
| `IntrospectAsync`                               | `POST /realms/{realm}/protocol/openid-connect/token/introspect`    |
| `RevokeRefreshTokenAsync`                       | Same as `LogoutAsync`                                              |
| `GetUserInfoAsync`                              | `GET /realms/{realm}/protocol/openid-connect/userinfo`             |
| `CreateUserAsync` / `UpdateUserAsync` / `DeleteUserAsync` | Admin REST API via `KeycloakAdminClient`                  |
| `ChangePasswordAsync` / `ResetPasswordAsync`    | `PUT /admin/realms/{realm}/users/{id}/reset-password`              |
| `ListSessionsAsync` / `RevokeSessionAsync`      | `/admin/realms/{realm}/users/{id}/sessions` and `/sessions/{id}`   |
| `GetRolesAsync` / `CreateRolesAsync`            | `/admin/realms/{realm}/roles`                                      |
| `AssignRolesToUserAsync` / `RemoveRolesFromUserAsync` | `/admin/realms/{realm}/users/{id}/role-mappings/realm`       |
| `MfaChallengeAsync`                             | Returns a fresh challenge id; verification is part of the token grant via `LoginRequest.MfaCode` |
| `MfaVerifyAsync` / `CreateScopeAsync`           | `NotSupportedException` — Keycloak handles these flows differently |

## Configuration

```json
{
  "Firefly": {
    "Idp": {
      "Keycloak": {
        "ServerUrl":      "https://kc.example.com",
        "Realm":          "myrealm",
        "ClientId":       "myapp",
        "ClientSecret":   "<optional confidential client secret>",
        "AdminUsername":  "<optional, enables admin REST>",
        "AdminPassword":  "<optional>"
      }
    }
  }
}
```

When `AdminUsername` / `AdminPassword` are configured the adapter
authenticates with the admin REST API using a service-account token;
otherwise the admin operations throw `InvalidOperationException` at
runtime.

## Wiring

```csharp
builder.Services.Configure<KeycloakOptions>(builder.Configuration.GetSection(KeycloakOptions.SectionName));
builder.Services.AddHttpClient<KeycloakIdpAdapter>();
builder.Services.AddHttpClient<KeycloakAdminClient>();
builder.Services.AddSingleton<IIdpAdapter, KeycloakIdpAdapter>();
```

## Dependencies

| Reference                       | Used for             |
|---------------------------------|----------------------|
| `FireflyFramework.Idp`          | `IIdpAdapter`        |
| `Microsoft.Extensions.Http`     | `HttpClient` factory |
| `Microsoft.Extensions.Options`  | Bound options        |

## Java mapping

| .NET                     | Java                                                   |
|--------------------------|--------------------------------------------------------|
| `KeycloakIdpAdapter`     | `KeycloakIdpAdapter` (`fireflyframework-idp-keycloak`) |
| `KeycloakAdminClient`    | `KeycloakAPIFactory`                                   |
| `KeycloakOptions`        | `KeycloakProperties`                                   |
