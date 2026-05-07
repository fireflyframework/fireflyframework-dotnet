# FireflyFramework.Idp

Identity-provider abstraction for Firefly Framework on .NET. Mirrors
`org.fireflyframework:firefly-idp`.

## Overview

`FireflyFramework.Idp` is the **port** project of the framework's
identity layer. It defines a single, opinionated interface — `IIdpAdapter` —
that captures the full lifecycle of authentication, identity, user
management, role / scope management, sessions, and multi-factor
authentication. The abstraction is deliberately neutral: nothing about
the contract leaks the underlying provider, so application code stays
free of vendor-specific types.

The hub-and-spoke design follows hexagonal architecture. Application
code depends only on `IIdpAdapter` and the request/response records in
`Dtos.cs`. Concrete adapters — `Idp.Keycloak`, `Idp.AwsCognito`,
`Idp.AzureAd`, `Idp.InternalDb` — implement the port against a real
provider. Switching providers becomes a configuration change, not a
code change. This is the same shape as the Java `firefly-idp` module
and its sibling adapter packages.

The contract is intentionally **wider than what any single provider can
fulfil**. Some operations are universally supported (`LoginAsync`,
`RefreshAsync`, `GetUserInfoAsync`); others are provider-specific
(per-session listing exists in Keycloak but not in Cognito or Microsoft
Graph). The framework's rule is: surface the full intent of the
contract, then have each adapter throw `NotSupportedException` with a
**documented remediation message** when the upstream provider has no
equivalent. There are no silent no-ops, and there is no guessing about
which methods are real on a given backend.

This project ships only the contract and the DTOs. It carries no I/O,
no SDK references, and no dependencies beyond
`FireflyFramework.Kernel`. That is what makes it safe to reference from
any layer of an application.

## When to use this module

Reference this project directly when:

- You are writing **application code** that needs to authenticate users,
  fetch identity information, manage users / roles, or check sessions —
  but should not know whether you ultimately run on Keycloak, Cognito,
  Entra ID, or your own database.
- You are writing a **new IdP adapter**. Implement `IIdpAdapter` and
  follow the conventions of the existing adapters (throw
  `NotSupportedException` for upstream gaps, never silently no-op).
- You are writing **integration tests** that need to substitute the
  identity layer. Mock `IIdpAdapter` and the rest of the framework keeps
  working.

Avoid referencing it from anything performance-critical that benefits
from a vendor SDK type — those callers should depend on the concrete
adapter project (e.g. `FireflyFramework.Idp.Keycloak`) instead.

## Mental model

```
                   +------------------------+
                   |   Application code     |
                   | (controllers, sagas,   |
                   |  command handlers...)  |
                   +-----------+------------+
                               |
                               | IIdpAdapter (port)
                               v
                   +------------------------+
                   |   FireflyFramework.Idp |
                   |  (this project)        |
                   +-----------+------------+
                               |
            +------------------+--------------------+
            |                  |                    |
   +--------v-------+  +-------v--------+  +--------v-------+
   |   Keycloak     |  |    Cognito     |  |   Azure AD /   |
   |    adapter     |  |    adapter     |  |   Entra ID     |
   +----------------+  +----------------+  +----------------+
            |                  |                    |
   +--------v-------+
   |   InternalDb   |
   |    adapter     |
   +----------------+
```

A consumer registers exactly one adapter as the runtime `IIdpAdapter` in
DI. From that point on, every call to `LoginAsync`, `IntrospectAsync`,
`CreateUserAsync`, etc. is dispatched against that single adapter.

## Quick start

The smallest possible wire-up uses the InternalDb adapter:

```csharp
using FireflyFramework.Idp;
using FireflyFramework.Idp.InternalDb;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure adapter-specific options.
builder.Services.Configure<InternalDbOptions>(
    builder.Configuration.GetSection(InternalDbOptions.SectionName));

// 2. Provide the SPI implementations the adapter needs.
builder.Services.AddSingleton<IInternalUserRepository, MyEfCoreUserRepository>();

// 3. Register the adapter as the framework-wide IIdpAdapter.
builder.Services.AddSingleton<IIdpAdapter, InternalDbIdpAdapter>();

var app = builder.Build();

// 4. Use it from any handler / endpoint.
app.MapPost("/login", async (LoginRequest req, IIdpAdapter idp, CancellationToken ct) =>
{
    var token = await idp.LoginAsync(req, ct);
    return Results.Ok(token);
});

app.Run();
```

Switching to Keycloak is purely a registration change — `MyEfCoreUserRepository`
goes away, `KeycloakOptions` and `KeycloakIdpAdapter` come in.

## Public surface

### Port

| Type | One-line description |
|---|---|
| `IIdpAdapter` | The single interface every adapter implements. 19 explicit operations + 1 default-implemented `RegisterUserAsync` that delegates to `CreateUserAsync`. |

The full method list, grouped by concern:

```csharp
// Authentication
Task<TokenResponse>            LoginAsync(LoginRequest, CancellationToken);
Task<TokenResponse>            RefreshAsync(RefreshRequest, CancellationToken);
Task                           LogoutAsync(LogoutRequest, CancellationToken);
Task<IntrospectionResponse>    IntrospectAsync(string accessToken, CancellationToken);
Task                           RevokeRefreshTokenAsync(string refreshToken, CancellationToken);

// Identity
Task<UserInfoResponse>         GetUserInfoAsync(string accessToken, CancellationToken);

// User management
Task<CreateUserResponse>       CreateUserAsync(CreateUserRequest, CancellationToken);
Task<UpdateUserResponse>       UpdateUserAsync(UpdateUserRequest, CancellationToken);
Task                           DeleteUserAsync(string userId, CancellationToken);

// Password
Task                           ChangePasswordAsync(ChangePasswordRequest, CancellationToken);
Task                           ResetPasswordAsync(string userId, CancellationToken);

// Multi-factor
Task<MfaChallengeResponse>     MfaChallengeAsync(string userId, CancellationToken);
Task<TokenResponse>            MfaVerifyAsync(MfaVerifyRequest, CancellationToken);

// Sessions
Task<IReadOnlyList<SessionInfo>> ListSessionsAsync(string userId, CancellationToken);
Task                             RevokeSessionAsync(string userId, string sessionId, CancellationToken);

// Roles
Task<IReadOnlyList<string>>    GetRolesAsync(CancellationToken);
Task<CreateRolesResponse>      CreateRolesAsync(CreateRolesRequest, CancellationToken);
Task                           AssignRolesToUserAsync(AssignRolesRequest, CancellationToken);
Task                           RemoveRolesFromUserAsync(AssignRolesRequest, CancellationToken);

// Scopes
Task<CreateScopeResponse>      CreateScopeAsync(CreateScopeRequest, CancellationToken);

// Self-service registration (default delegates to CreateUserAsync)
Task<CreateUserResponse>       RegisterUserAsync(RegisterUserRequest, CancellationToken);
```

### DTOs

Every request and response is a positional `record` so you get value
equality, deconstruction, `with`-expressions, and immutability for free.

| DTO | Purpose |
|---|---|
| `LoginRequest(Username, Password, MfaCode?)` | Credentials grant. `MfaCode` is the TOTP/SMS code when the IdP wants both factors in a single call. |
| `TokenResponse(AccessToken, RefreshToken?, TokenType, ExpiresIn, Scope?, IdToken?)` | The access + refresh JWTs (or opaque tokens) and OIDC niceties. |
| `RefreshRequest(RefreshToken)` | Refresh-token grant. |
| `LogoutRequest(RefreshToken)` | The token to invalidate. |
| `IntrospectionResponse(Active, Username?, Sub?, Roles?, Claims?)` | RFC 7662-shaped result, neutral over actual implementation. |
| `UserInfoResponse(Sub, Email?, GivenName?, FamilyName?, Roles?, Claims?)` | OIDC-style userinfo. |
| `CreateUserRequest(Username, Email, Password?, GivenName?, FamilyName?, Roles?, Attributes?)` | Admin user creation. `Password` is null when the IdP issues a temporary password. |
| `CreateUserResponse(UserId)` / `UpdateUserResponse(UserId)` | Identifier round-tripped after admin operations. |
| `UpdateUserRequest(UserId, Email?, GivenName?, FamilyName?, Attributes?)` | Patch-shaped update; null fields are not modified. |
| `ChangePasswordRequest(UserId, OldPassword, NewPassword)` | Self-service password change. |
| `CreateRolesRequest` / `CreateRolesResponse` | Bulk role creation. |
| `AssignRolesRequest(UserId, RoleNames)` | Used for both assign and remove. |
| `CreateScopeRequest` / `CreateScopeResponse` | OAuth2 scope creation. Provider-specific support. |
| `MfaChallengeResponse(ChallengeId, Method)` | Provider-issued challenge handle. |
| `MfaVerifyRequest(ChallengeId, Code)` | Verifies the code against the issued challenge. |
| `SessionInfo(SessionId, UserId, CreatedAt, LastActivity?, IpAddress?, UserAgent?)` | Per-session metadata. |
| `RegisterUserRequest(Username, Email, Password, GivenName?, FamilyName?)` | Self-service signup variant. |

### Default implementations

The port supplies one default method:

```csharp
Task<CreateUserResponse> RegisterUserAsync(RegisterUserRequest request, CancellationToken ct = default) =>
    CreateUserAsync(new CreateUserRequest(request.Username, request.Email, request.Password,
        request.GivenName, request.FamilyName, null, null), ct);
```

This means new adapters do **not** have to implement `RegisterUserAsync`
unless they want a different behaviour. The default delegates to the
admin path with no roles and no extra attributes.

## Configuration

The port project itself has no configuration — every knob lives on the
chosen adapter. See the README for `Idp.Keycloak`, `Idp.AwsCognito`,
`Idp.AzureAd`, or `Idp.InternalDb`.

## Common patterns

### Pattern 1: Provider-agnostic login endpoint

The whole point of the port is that your handler doesn't know or care
which adapter is wired up.

```csharp
app.MapPost("/auth/login", async (LoginRequest req, IIdpAdapter idp, CancellationToken ct) =>
{
    try
    {
        var token = await idp.LoginAsync(req, ct);
        return Results.Ok(token);
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
});
```

### Pattern 2: Token introspection middleware

`IntrospectAsync` returns a normalized `IntrospectionResponse`. Use it
in a custom middleware to attach claims to `HttpContext.User` regardless
of provider.

```csharp
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Headers.TryGetValue("Authorization", out var hdr) &&
        hdr.ToString().StartsWith("Bearer "))
    {
        var token = hdr.ToString()["Bearer ".Length..];
        var idp = ctx.RequestServices.GetRequiredService<IIdpAdapter>();
        var result = await idp.IntrospectAsync(token, ctx.RequestAborted);
        if (result.Active)
        {
            // Attach claims, log audit, etc.
        }
    }

    await next();
});
```

### Pattern 3: Self-service registration

`RegisterUserAsync` has a default implementation that delegates to
`CreateUserAsync`. Adapters override it only when self-service has a
different lifecycle (e.g. email verification before activation).

```csharp
await idp.RegisterUserAsync(new RegisterUserRequest("alice", "alice@x.com",
    "Sup3r$ecret!", "Alice", "Smith"), ct);
```

### Pattern 4: Bulk role assignment

```csharp
await idp.CreateRolesAsync(new CreateRolesRequest(new[] { "admin", "auditor" }), ct);
await idp.AssignRolesToUserAsync(new AssignRolesRequest(userId, new[] { "admin" }), ct);
```

## Pitfalls and gotchas

- **Do not assume every method is supported.** Some adapters throw
  `NotSupportedException` because the upstream provider does not expose
  the operation. Always read the adapter's coverage table before
  designing a flow that depends on, say, `ListSessionsAsync` or
  `MfaVerifyAsync` against a particular IdP.
- **`MfaCode` belongs on `LoginRequest`.** The Keycloak adapter expects
  the TOTP code in `LoginRequest.MfaCode` rather than as a separate
  `MfaVerifyAsync` call. Other providers reject MFA mid-grant. Read each
  adapter's notes.
- **`CreateScopeAsync` is rarely runtime.** Most IdPs only allow scope
  creation from the admin console / IaC. Cognito and Azure AD throw
  `NotSupportedException`; Keycloak does too because scopes are realm-level.
- **`IntrospectionResponse.Active` is informative, not authoritative.**
  Some providers (Cognito, Azure AD) have no real introspection
  endpoint; the adapters synthesize a best-effort answer. For cryptographic
  validation you still need JWT signature verification at the edge.
- Treat `RegisterUserRequest` as suggestion, not contract. The default
  implementation drops `Roles` and `Attributes`; only override when the
  adapter genuinely supports differentiated registration paths.

## Internals (for the curious)

The port is deliberately **interface-only with default-implemented
sugar**. The framework does not own the abstract base class for
adapters — every adapter is a `sealed class` that implements the
interface directly. This keeps the dependency graph flat and avoids the
classic OOP trap of feature flags creeping into a base class.

DTOs are positional `record`s. Choosing records over `class` gives us:

1. Free value equality (good for tests with `Assert.Equal`).
2. Free deconstruction (`var (sub, email, _, _, _, _) = info;`).
3. `with`-expressions for immutable "edits" (`req with { Password = "new" }`).
4. Compiler-enforced non-nullable contracts.

The port project has zero I/O dependencies — referencing it from a
shared library never drags in HTTP, SDKs, or telemetry. That keeps the
abstraction transitively safe to use from any layer.

## Dependencies

| Reference | Why it's there |
|---|---|
| `FireflyFramework.Kernel` | Base exceptions; nothing else. |
| `Microsoft.Extensions.Options` | `IOptions<T>` shape (used by every adapter — declared here so adapters don't all redeclare it). |
| `Microsoft.Extensions.Logging.Abstractions` | `ILogger<T>` shape, same reason. |

## Java mapping

| .NET | Java |
|---|---|
| `IIdpAdapter` | `IdpAdapter` |
| `LoginRequest` / `TokenResponse` / `RefreshRequest` / `LogoutRequest` | matching DTOs (no `Dto` suffix) |
| `IntrospectionResponse` / `UserInfoResponse` | matching DTOs |
| `CreateUserRequest` / `UpdateUserRequest` / `ChangePasswordRequest` | matching DTOs |
| `MfaChallengeResponse` / `MfaVerifyRequest` | matching DTOs |
| `CreateRolesRequest` / `AssignRolesRequest` / `CreateScopeRequest` | matching DTOs |
| `SessionInfo` / `RegisterUserRequest` | matching DTOs |
