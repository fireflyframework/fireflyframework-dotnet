# FireflyFramework.Kernel

The bedrock of the framework. Every other Firefly project transitively
references **FireflyFramework.Kernel**, and Kernel itself references
nothing — not even another Firefly project, not even a third-party
NuGet. It is the single source of truth for two cross-cutting
concerns that everything above it depends on: a **typed exception
hierarchy** with stable error codes, and a **calendar-version
constant** that aligns the .NET line with the Java release train.

Mirrors `org.fireflyframework:firefly-common` (Java).

---

## Why a separate kernel project at all?

Many .NET projects collapse this kind of bottom-of-the-stack code into
their main library. We don't, for three reasons:

1. **Dependency direction.** Higher tiers (Web, CQRS, EDA, the
   adapters, the starters) all need to throw and catch
   `FireflyException` without circularly referencing each other.
   Putting the exception hierarchy in a leaf project breaks the
   dependency cycle naturally.
2. **Trivial to reason about.** This project has zero
   `<PackageReference>` and zero `<ProjectReference>`. There's nothing
   to upgrade, nothing to break. A consumer that only wants the
   exception types can pull this single ~12-KB DLL.
3. **Testable in isolation.** No stubs, no mocks, no fixtures. A
   pure-CLR test of `FireflyException.WithContext("k", v)` runs in
   sub-millisecond time and never touches the file system or the
   network.

Treat Kernel as a **leaf** in the dependency graph: it depends on
nothing, the rest of the framework depends on it.

---

## Mental model

```
                ┌──────────────────────────────────────┐
                │           Exception                  │
                │       (System namespace)             │
                └──────────────────┬───────────────────┘
                                   │
                ┌──────────────────▼───────────────────┐
                │         FireflyException             │
                │   ErrorCode + Context dictionary     │
                └──────┬─────────────────────┬─────────┘
                       │                     │
        ┌──────────────▼────────────┐  ┌────▼──────────────────────┐
        │ FireflyInfrastructureExc. │  │  FireflySecurityException │
        │  DB / cache / messaging   │  │   AuthN / AuthZ failure   │
        └───────────────────────────┘  └───────────────────────────┘
```

`FireflyException` is the root. Two specialised subclasses cover the
two domains that benefit most from being distinguishable in catch
sites: **infrastructure** (transient, retryable, often raised by
adapters) and **security** (intentional reject, never retried, must be
audited).

Application-layer code is encouraged to throw subclasses defined
elsewhere in the framework — `FireflyFramework.Web` ships
`BusinessException`, `ValidationException`, and a family of
HTTP-typed exceptions all rooted at `FireflyException` so a single
global handler can map them to RFC 7807 problem responses.

---

## Public surface

### `FireflyException`

The framework's root exception type. Every Firefly-thrown error
ultimately inherits from this; every catch-all in the framework's own
middleware filters on it.

| Member | Type | Description |
|---|---|---|
| `ErrorCode` | `string` | Stable, machine-readable identifier (e.g. `"FIREFLY_ERROR"`, `"DB_UNREACHABLE"`). Carried into RFC 7807 problem JSON by the Web layer; used by log filters, alerting rules, and on-call runbooks. Never `null` — defaults to `"FIREFLY_ERROR"`. |
| `Context` | `IReadOnlyDictionary<string, object?>` | Free-form diagnostic bag. Use it to attach the offending input, the upstream URL, the correlation ID — anything the catch site might want without subclassing the exception. Defaults to an empty dictionary. |
| `WithContext(string, object?)` | `FireflyException` | Returns a *copy* of the exception with one extra context entry. Use this when an outer layer wants to enrich an exception without mutating the original or losing its inner exception. |

#### Constructor matrix

The class exposes six constructors so call sites can pick the shape
that fits without writing boilerplate:

```csharp
new FireflyException();                                              // empty + default code
new FireflyException("payment failed");                              // message + default code
new FireflyException("payment failed", "PAY_002");                   // message + explicit code
new FireflyException("payment failed", inner);                       // message + cause
new FireflyException("payment failed", "PAY_002", inner);            // message + code + cause
new FireflyException("payment failed", "PAY_002", ctx, inner);       // full ribbon
```

The full-ribbon form is what `WithContext` uses internally and what
adapters typically call when they want to attach the full context
dictionary at throw time.

### `FireflyInfrastructureException`

Use this for **transient, retryable, infrastructure-side failures**:

* Connection-pool exhausted, socket timeout, broken pipe.
* Database deadlock, optimistic-concurrency conflict, replica out of sync.
* Cache miss-storm, Redis cluster fail-over.
* Kafka rebalance, RabbitMQ confirm timeout.

Default error code: `"FIREFLY_INFRASTRUCTURE_ERROR"`. The Web layer's
RFC 7807 mapper translates this into HTTP 503 by default; consumer
applications can override via the converter SPI.

```csharp
try
{
    await _connection.OpenAsync(ct);
}
catch (NpgsqlException ex) when (ex.IsTransient)
{
    throw new FireflyInfrastructureException(
        $"primary database is unreachable for tenant {tenantId}",
        errorCode: "DB_UNREACHABLE",
        cause: ex);
}
```

### `FireflySecurityException`

Use this for **deliberate authn / authz rejects**:

* Missing or invalid bearer token.
* Permission denied — caller authenticated but not entitled.
* Tenant mismatch — caller's tenant differs from the resource's.
* Refresh-token revocation.

Default error code: `"FIREFLY_SECURITY_ERROR"`. Maps to HTTP 401 / 403
in the Web layer depending on the converter chain.

```csharp
if (!user.HasRole("orders:write"))
{
    throw new FireflySecurityException(
        $"user '{user.Id}' is not entitled to write orders",
        errorCode: "ORDERS_FORBIDDEN");
}
```

### `FireflyVersion`

A single `public const string Current` value carrying the calendar
version (`"26.04.01"`). Kept in lockstep with
`fireflyframework-parent/pom.xml` on the Java side so
`FireflyVersion.Current` on .NET matches the published Java framework
version a service is running against.

```csharp
logger.LogInformation(
    "Firefly Framework {Version} starting on {Runtime}",
    FireflyVersion.Current,
    RuntimeInformation.FrameworkDescription);
```

The framework's startup banner (`FireflyFramework.Web/Logging/FireflyBanner.cs`)
reads this constant — there is exactly one place to bump the version
when releasing.

---

## Common patterns

### Enriching an exception across a layer boundary

A repository throws a tight, tenant-blind
`FireflyInfrastructureException`. The application layer wants to add
the tenant identifier without subclassing or reconstructing the
exception:

```csharp
try
{
    return await _repo.LoadAsync(orderId, ct);
}
catch (FireflyException ex)
{
    throw ex.WithContext("tenantId", tenantContext.Current);
}
```

`WithContext` returns a *new* `FireflyException` that preserves the
original message, error code, and inner exception. It does *not*
mutate the original — `Context` is immutable by design, so the
original instance is safe to log or rethrow elsewhere unchanged.

### Catching with type discrimination

The two subclasses exist precisely so that retry / circuit-breaker
logic can distinguish infrastructure failures from policy rejects:

```csharp
try
{
    await _innerHandler.HandleAsync(cmd, ctx, ct);
}
catch (FireflyInfrastructureException) when (attempts < maxRetries)
{
    // Transient. Back off and retry.
    await Task.Delay(backoff, ct);
    attempts++;
    continue;
}
catch (FireflySecurityException)
{
    // Deliberate reject. Never retry.
    throw;
}
```

### Composing with the Polly resilience pipeline

`FireflyInfrastructureException` is a sensible default for the
*transient* predicate in a Polly retry policy:

```csharp
var pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        ShouldHandle = new PredicateBuilder()
            .Handle<FireflyInfrastructureException>(),
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
    })
    .Build();
```

`FireflySecurityException` is deliberately *not* in the retry
predicate — retrying an authentication failure would just delay the
inevitable and waste server-side rate-limit budget.

---

## Pitfalls and gotchas

**Don't mutate the `Context` dictionary.** It is exposed as
`IReadOnlyDictionary<string, object?>` precisely so that the catch
site can't accidentally change shared state. Use `WithContext` to
produce a derived copy. The dictionary returned at construction time
is also a *defensive copy* of the dictionary you passed in — modifying
your local copy after throwing has no effect on the exception.

**Don't put PII directly into `Context`.** The `Context` dictionary
is propagated all the way to the Web layer's RFC 7807 response.
`FireflyFramework.Web/PiiMaskingService` masks well-known keys
(`email`, `phone`, `ssn`, …) before serialisation, but anything
outside that list is rendered verbatim. If you must attach a
credit-card number for debugging, mask it at the throw site.

**Don't subclass `FireflyException` in a service project.** The
framework already exposes a richer hierarchy in
`FireflyFramework.Web/Errors/Exceptions/` — `BusinessException`,
`ValidationException`, the HTTP-typed family. Subclassing makes those
specialised converters miss your exception. Use a stable
`ErrorCode` instead.

**Don't read `FireflyVersion.Current` to gate features.** The
constant is for diagnostics and bannering, not for runtime
feature-flag decisions. Calendar versions don't carry semantic meaning
about feature presence — use a real feature flag.

---

## Internals (for the curious)

`FireflyException` stores the context dictionary as a defensive copy
because callers occasionally pass a mutable dictionary they continue
to write to after throwing. Treating the input as borrowed and copying
once at construction is the cheapest way to avoid spooky action at a
distance, and the construction cost is dwarfed by the cost of stack
walking.

`WithContext` allocates a fresh dictionary instead of using
`ImmutableDictionary` because the median context size is 0–3 entries
where the constant factor of `ImmutableDictionary` actually outweighs
the cost of copying. We profiled both and went with the simpler one.

The default error codes (`FIREFLY_ERROR`,
`FIREFLY_INFRASTRUCTURE_ERROR`, `FIREFLY_SECURITY_ERROR`) are
screaming-snake-case to match the Java framework's convention so log
searches across both runtimes use the same string. We deliberately did
not choose ASP.NET Core's camelCase Problem Details `type` URI
convention for the error code — that's a different field with a
different role.

---

## Dependencies

**None.** The csproj has neither `<ProjectReference>` nor
`<PackageReference>`. Building Kernel is a pure
`csc → FireflyFramework.Kernel.dll` invocation that pulls only the
.NET 10 base class library.

This is enforced — the `Directory.Build.props` cascade does not
inject any package references, and the project's csproj is checked
into the repository as a one-line `<Project Sdk="Microsoft.NET.Sdk">`
with the `<PackageId>` property set.

---

## Java mapping

| .NET | Java original |
|---|---|
| `FireflyFramework.Kernel.Exceptions.FireflyException` | `org.fireflyframework.kernel.exception.FireflyException` |
| `FireflyFramework.Kernel.Exceptions.FireflyInfrastructureException` | `org.fireflyframework.kernel.exception.FireflyInfrastructureException` |
| `FireflyFramework.Kernel.Exceptions.FireflySecurityException` | `org.fireflyframework.kernel.exception.FireflySecurityException` |
| `FireflyFramework.Kernel.FireflyVersion.Current` | `${revision}` property in `fireflyframework-parent/pom.xml` |

The error codes and context-dictionary semantics are wire-identical
on both runtimes, which is what makes the `application/problem+json`
shape produced by the Web layer interchangeable across Java and .NET
services in the same platform.

---

## See also

* [`FireflyFramework.Web`](../FireflyFramework.Web/README.md) — RFC 7807 mapping, the richer exception family (`BusinessException`, `ValidationException`, HTTP-typed exceptions).
* [`docs/ARCHITECTURE.md`](../../docs/ARCHITECTURE.md) — where Kernel sits in the four-tier framework layering.
* [`docs/MIGRATION-GUIDE.md`](../../docs/MIGRATION-GUIDE.md) — how Java exception types map to their .NET counterparts.
