# FireflyFramework.Security

Spring Security port for the .NET stack. Wraps ASP.NET Core
authentication/authorization with Firefly-flavored abstractions:
`SecurityContext`, `ISecurityContextHolder`, declarative `[PreAuthorize]`,
`IPasswordEncoder`, `IJwtTokenService`.

## Why a separate module?

The IDP module abstracts *external* identity providers (Keycloak,
Cognito, Azure AD). The Security module is the in-process
authentication/authorization layer — what runs after the token has been
verified, what `ClaimsPrincipal` looks like to your handlers, what the
declarative authorization rules say. Spring keeps these in the same
project (`spring-security`); the .NET split mirrors how
`fireflyframework-idp` and Spring Security differ in scope.

## Quick start

```csharp
services.AddFireflySecurity(Configuration);

var app = builder.Build();
app.UseAuthentication();
app.UseFireflySecurityContext();   // populates ISecurityContextHolder.Current
app.UseAuthorization();
```

```yaml
Firefly:
  Security:
    Jwt:
      Issuer: firefly
      Audience: orders-api
      Secret: ${FIREFLY_JWT_SECRET}
      AccessTokenLifetime: 01:00:00
    Password:
      Encoder: BCrypt
      BCryptWorkFactor: 12
```

```csharp
[PreAuthorize("hasRole('ADMIN')")]
public async Task<Result> ApproveAsync(...) { ... }
```
