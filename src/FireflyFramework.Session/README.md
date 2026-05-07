# FireflyFramework.Session

Spring Session port. `IFireflySession` is the per-request handle;
`ISessionStore` is the persistence port (in-memory or Redis); the
middleware reads the session cookie, hydrates the session, hands the
request the live object via `HttpContext.GetFireflySession()`, and
flushes back on the way out.

## Why a Firefly session vs the built-in ASP.NET Core session?

ASP.NET Core's `app.UseSession()` is keyed on a single `IDistributedCache`
and gives you `byte[]`-indexed access. The Firefly session matches the
pyfly / Spring contract: typed `Get<T>` / `Set<T>`, `IsExpired`, an
explicit `MaxInactiveInterval`, and pluggable backing stores via the
same hexagonal port pattern as cache and IDP.

## Quick start

```csharp
services.AddFireflySession(Configuration);

app.UseFireflySession();   // before MVC

app.MapGet("/cart", ctx =>
{
    var s = ctx.GetFireflySession();
    s!.TryGet<Cart>("cart", out var cart);
    return Results.Json(cart);
});
```

```yaml
Firefly:
  Session:
    Provider: Redis              # or Memory
    CookieName: FIREFLY_SESSION
    SecureCookie: true
    MaxInactiveInterval: 00:30:00
    Redis:
      ConnectionString: localhost:6379
      KeyPrefix: firefly:session:
```
