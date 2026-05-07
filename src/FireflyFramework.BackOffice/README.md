# FireflyFramework.BackOffice

## Overview

`FireflyFramework.BackOffice` is the **back-office tier** for
internal admin and customer-impersonation routes. It layers a
request-scoped `BackofficeContext` resolver and middleware on top of
`Starter.Application`, so admin endpoints can read the operator's
identity, the impersonated customer, the contract / product
context, and roles / permissions without restating the parsing
logic in every controller.

Mirrors `org.fireflyframework:firefly-backoffice` from the Java line.

## Why a separate module?

Back-office routes carry very different security semantics from
end-user routes:

- The caller is a *staff* user, not the customer.
- The action is performed *on behalf of* a customer (impersonation).
- The audit story is rich: every back-office action carries the
  staff user, the impersonated customer, the reason, and the IP.
- Authorisation is role/permission-based against an internal
  security catalogue, not a generic OAuth scope.

Putting these concerns in their own module keeps end-user code
clean and lets the back-office layer evolve independently
(impersonation rules, MFA-elevation prompts, time-bounded grants).

## Mental model

```
   ASP.NET pipeline:
        │
        │   inbound request with headers:
        │   X-User-Id, X-Impersonate-Party-Id,
        │   X-Tenant-Id, X-Impersonation-Reason
        ▼
   ┌──────────────────────────────────┐
   │ BackofficeMiddleware             │
   │   IBackofficeContextResolver     │  → builds BackofficeContext
   │   stores in HttpContext.Items    │
   └──────────────┬───────────────────┘
                  │
                  ▼
   ┌──────────────────────────────────┐
   │ controller / endpoint            │
   │   var ctx = http.GetBackoffice() │
   │   if (!ctx.HasRole(...)) Forbid  │
   └──────────────────────────────────┘
```

Default resolver reads headers populated by the service mesh
(Istio / sidecar proxy) which has already authenticated the staff
user; subclass it to load roles from your security centre.

## Quick start

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
await app.RunAsync();
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
    if (!ctx.IsValidImpersonation())
    {
        return Results.BadRequest("Missing or invalid impersonation context.");
    }
    return Results.Ok(/* ... */);
});
```

## Public surface

### `BackofficeContext`

Immutable record with role / permission helpers:

| Property                                   | Source                                                                    |
|--------------------------------------------|---------------------------------------------------------------------------|
| `BackofficeUserId`                         | `X-User-Id` header (Istio injection)                                      |
| `ImpersonatedPartyId`                      | `X-Impersonate-Party-Id` header                                           |
| `TenantId`                                 | `X-Tenant-Id` header                                                      |
| `ImpersonationReason`                      | `X-Impersonation-Reason` header                                           |
| `BackofficeUserIpAddress`                  | `HttpContext.Connection.RemoteIpAddress`                                  |
| `ContractId`, `ProductId`                  | Supplied by the controller via the resolver overload                      |
| `BackofficeRoles` / `BackofficePermissions`| Populated by your subclass of `HeaderBackofficeContextResolver`           |

Helpers:

| Method                              | Returns                                                       |
|-------------------------------------|---------------------------------------------------------------|
| `HasBackofficeRole(role)`           | True if role is present                                       |
| `HasAnyBackofficeRole(...)`         | True if any of the supplied roles is present                  |
| `HasAllBackofficeRoles(...)`        | True if all of the supplied roles are present                 |
| `HasBackofficePermission(perm)`     | True if permission is present                                 |
| `ImpersonatedPartyHasRole(role)`    | True if the impersonated party itself carries the role        |
| `HasContract()` / `HasProduct()`    | True if the corresponding id is set                           |
| `GetAttribute<T>(key)`              | Reads from the resolver-populated attribute bag               |
| `IsValidImpersonation()`            | True if both BackofficeUserId and ImpersonatedPartyId are set |

### `BackofficeSecurityContext`

Per-endpoint security record: required roles / permissions,
authorization outcome, optional `SecurityEvaluationResult` and
`ImpersonationAuditTrail`. Used by the framework's authorisation
filters to record what was checked and why.

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
Subclass it to load roles / permissions from your security centre
and override `ValidateImpersonationAsync` to enforce policy:

```csharp
public sealed class SecurityCenterResolver(
    HttpContext _, ISecurityCenter security)
    : HeaderBackofficeContextResolver
{
    public override async Task<BackofficeContext> ResolveAsync(
        HttpContext http, CancellationToken ct = default)
    {
        var basic = await base.ResolveAsync(http, ct);
        var roles = await security.GetRolesAsync(basic.BackofficeUserId!.Value, ct);
        var perms = await security.GetPermissionsAsync(basic.BackofficeUserId!.Value, ct);
        return basic with
        {
            BackofficeRoles       = roles,
            BackofficePermissions = perms,
        };
    }

    public override async Task<bool> ValidateImpersonationAsync(
        Guid backofficeUserId, Guid impersonatedPartyId,
        HttpContext httpContext, CancellationToken ct = default)
    {
        var grant = await security.GetImpersonationGrantAsync(backofficeUserId, impersonatedPartyId, ct);
        return grant is { ExpiresAt: > DateTimeOffset.UtcNow };
    }
}
```

### Middleware

`BackofficeMiddleware` (mounted by `UseFireflyBackoffice`) calls the
resolver on every request and stores the result in
`HttpContext.Items["Firefly.BackofficeContext"]`. Read it via
`HttpContext.GetBackofficeContext()`.

## Common patterns

### Authorising by role + permission

```csharp
var ctx = http.GetBackofficeContext()!;
if (!ctx.HasAllBackofficeRoles("customer_support", "tier2"))    return Results.Forbid();
if (!ctx.HasBackofficePermission("customer.refund"))            return Results.Forbid();
```

### Auditing every back-office action

```csharp
public sealed class BackofficeAuditFilter(IAuditService audit) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext ctx, ActionExecutionDelegate next)
    {
        var bo = ctx.HttpContext.GetBackofficeContext()!;
        var sw = Stopwatch.StartNew();
        var result = await next();
        await audit.RecordAsync(new BackofficeAuditRecord(
            BackofficeUserId:    bo.BackofficeUserId,
            ImpersonatedPartyId: bo.ImpersonatedPartyId,
            Action:              ctx.ActionDescriptor.DisplayName,
            DurationMs:          sw.ElapsedMilliseconds,
            Result:              result.Exception is null ? "ok" : "error"), ctx.HttpContext.RequestAborted);
    }
}
```

### Customer impersonation guard

```csharp
public sealed class RequireValidImpersonationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
    {
        var bo = ctx.HttpContext.GetBackofficeContext();
        if (bo is null || !bo.IsValidImpersonation())
        {
            ctx.Result = new BadRequestObjectResult("Impersonation context required.");
            return;
        }
        await next();
    }
}
```

## Pitfalls and gotchas

- **The headers must be set by an upstream component.** Don't trust
  them from arbitrary callers — route back-office traffic through a
  service mesh (Istio, Linkerd) that authenticates the staff user
  and injects the headers. Without that guard, anyone who can hit
  the endpoint can claim any user id.
- **`HasBackofficeRole(...)` is case-sensitive.** Match your
  security catalogue's casing exactly. Most centres use lowercase
  snake_case (`customer_support`, `tier2`).
- **`X-Impersonation-Reason` is mandatory for compliance.** Without
  it, an audit log can't justify why the staff member accessed the
  customer's data. Reject requests that lack it.
- **`BackofficeUserIpAddress` may be `127.0.0.1` behind a proxy.**
  Wire `UseForwardedHeaders` so `RemoteIpAddress` reflects the real
  origin.
- **Don't read context outside `UseFireflyBackoffice` scope.** A
  middleware ordering mistake (middleware after `MapControllers`)
  leaves the items dictionary empty.

## Internals (for the curious)

- `BackofficeContext` is a `sealed record` so it's value-equal and
  `with`-mutable. The resolver returns a new instance per request.
- The middleware stores the context under
  `HttpContext.Items["Firefly.BackofficeContext"]`. The accessor
  extension `GetBackofficeContext()` does the cast.
- The default resolver throws on missing required headers because
  *failing closed* is the right answer for back-office traffic — a
  silently-empty context would defeat impersonation auditing.

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
