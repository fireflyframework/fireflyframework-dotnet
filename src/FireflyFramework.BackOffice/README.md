# FireflyFramework.BackOffice

Back-office tier — context resolution and security primitives for
internal admin / customer-impersonation routes. Layers a request-scoped
`BackofficeContext` onto `Starter.Application`.

Mirrors `org.fireflyframework:firefly-backoffice`.

## Usage

```csharp
using FireflyFramework.BackOffice;

builder.Services.AddFireflyBackOffice(
    builder.Configuration,
    serviceName:    "support-portal",
    serviceVersion: "1.0.0",
    cqrsAssemblies: new[] { typeof(Program).Assembly });

var app = builder.Build();
app.UseFireflyBackoffice();   // resolves and stashes BackofficeContext per request
app.MapControllers();
```

In endpoints / controllers, read the resolved context:

```csharp
app.MapGet("/admin/customers/{partyId:guid}", (Guid partyId, HttpContext http) =>
{
    var ctx = http.GetBackofficeContext()!;   // throws if middleware ran but failed
    if (!ctx.HasBackofficeRole("customer_support"))
    {
        return Results.Forbid();
    }
    return Results.Ok(/* ... */);
});
```

## Public surface

### `BackofficeContext`

Immutable record with role / permission helpers:

| Property                          | Source                                                                    |
|-----------------------------------|---------------------------------------------------------------------------|
| `BackofficeUserId`                | `X-User-Id` header (Istio injection)                                      |
| `ImpersonatedPartyId`             | `X-Impersonate-Party-Id` header                                           |
| `TenantId`                        | `X-Tenant-Id` header                                                      |
| `ImpersonationReason`             | `X-Impersonation-Reason` header                                           |
| `BackofficeUserIpAddress`         | `HttpContext.Connection.RemoteIpAddress`                                  |
| `ContractId`, `ProductId`         | Supplied by the controller via the resolver overload                      |
| `BackofficeRoles` / `BackofficePermissions` | Populated by your subclass of `HeaderBackofficeContextResolver` |

Helpers: `HasBackofficeRole(role)`, `HasAnyBackofficeRole(...)`,
`HasAllBackofficeRoles(...)`, `HasBackofficePermission(perm)`,
`ImpersonatedPartyHasRole(role)`, `HasContract()`, `HasProduct()`,
`GetAttribute<T>(key)`, `IsValidImpersonation()`.

### `BackofficeSecurityContext`

Per-endpoint security record: required roles / permissions,
authorization outcome, optional `SecurityEvaluationResult` and
`ImpersonationAuditTrail`.

### Resolver

```csharp
public interface IBackofficeContextResolver
{
    Task<BackofficeContext> ResolveAsync(HttpContext httpContext, CancellationToken ct = default);
    Task<BackofficeContext> ResolveAsync(HttpContext httpContext, Guid? contractId, Guid? productId, CancellationToken ct = default);
    Task<bool>              ValidateImpersonationAsync(Guid backofficeUserId, Guid impersonatedPartyId, HttpContext httpContext, CancellationToken ct = default);
    int  Priority { get; }
    bool Supports (HttpContext httpContext);
}

public class HeaderBackofficeContextResolver : IBackofficeContextResolver { ... }
```

`HeaderBackofficeContextResolver` reads the four headers above and
throws `InvalidOperationException` if the required ones are missing.
Subclass it to load roles / permissions from your security center and
override `ValidateImpersonationAsync` to enforce policy.

### Middleware

`BackofficeMiddleware` (mounted by `UseFireflyBackoffice`) calls the
resolver on every request and stores the result in
`HttpContext.Items["Firefly.BackofficeContext"]`. Read it via
`HttpContext.GetBackofficeContext()`.

## Dependencies

| Reference                                | Pulled in transitively  |
|------------------------------------------|-------------------------|
| `FireflyFramework.Starter.Application`   | always                  |
| `Microsoft.AspNetCore.App` (FrameworkRef)| Middleware, HttpContext |

## Java mapping

| .NET                                  | Java                                  |
|---------------------------------------|---------------------------------------|
| `BackofficeContext`                   | `BackofficeContext`                   |
| `BackofficeSecurityContext`           | `BackofficeSecurityContext`           |
| `IBackofficeContextResolver`          | `BackofficeContextResolver`           |
| `HeaderBackofficeContextResolver`     | `DefaultBackofficeContextResolver`    |
| `BackofficeMiddleware`                | `BackofficeContextWebFilter`          |
