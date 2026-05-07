# FireflyFramework.Cqrs

Async command and query buses with handler discovery, validation,
authorization, query result caching, fluent dispatch, and
event-driven cache invalidation. The default mediator for any
service whose application layer is structured around commands and
queries — and the foundation that the EDA, event-sourcing, and
saga modules build on.

Mirrors `org.fireflyframework:firefly-common-cqrs`.

---

## Why CQRS in the framework?

Three concerns recur in every non-trivial business service:

1. **Validation** — the request must be well-formed before any
   business rule runs.
2. **Authorization** — the caller must be entitled to do this
   particular thing on this particular resource.
3. **Caching of read paths** — the same read happens many times per
   second; we don't want to hit the database on every one.

In MVC controllers these concerns are scattered across filters,
attributes, and helper services that drift between teams. CQRS — the
pattern of routing every request through a typed *command* (write) or
*query* (read) and a single corresponding handler — gives the
framework a single point to enforce all three concerns consistently.

`FireflyFramework.Cqrs` ships:

* `ICommand<TResult>` / `ICommandHandler<TCommand, TResult>` — the
  write contract.
* `IQuery<TResult>` / `IQueryHandler<TQuery, TResult>` — the read
  contract, with caching baked in.
* `DefaultCommandBus` / `DefaultQueryBus` — orchestrators that run
  the validate → authorize → handle pipeline.
* Fluent dispatch helpers (`For(cmd).WithUser(...).ExecuteAsync()`).
* `EventDrivenCacheInvalidator` — keep query caches consistent by
  reacting to domain events.

The buses know about validation, authorization, and caching out of
the box. Your handlers know about the business rule. That's the
intended separation of concerns.

---

## Mental model

```
                  ┌────────────┐
                  │  Caller    │
                  └─────┬──────┘
                        │ SendAsync(cmd, ctx, ct)
                        ▼
                  ┌──────────────────────────────┐
                  │       DefaultCommandBus      │
                  │ ── ValidateAsync             │   throws CqrsValidationException → 400
                  │ ── AuthorizeAsync            │   throws CqrsAuthorizationException → 403
                  │ ── handler.HandleAsync       │
                  └─────────────┬────────────────┘
                                │
                                ▼
                  ┌──────────────────────────────┐
                  │   ICommandHandler<TCmd, TR>  │
                  └──────────────────────────────┘


                  ┌────────────┐
                  │  Caller    │
                  └─────┬──────┘
                        │ AskAsync(query, ctx, ct)
                        ▼
                  ┌──────────────────────────────┐
                  │       DefaultQueryBus        │
                  │ ── AuthorizeAsync            │
                  │ ── cache.GetAsync(key) ─────►│ ── if hit, return
                  │ ── handler.HandleAsync       │
                  │ ── cache.SetAsync(key, val)  │
                  └─────────────┬────────────────┘
                                │
                                ▼
                  ┌──────────────────────────────┐
                  │   IQueryHandler<TQry, TR>    │
                  └──────────────────────────────┘
```

The pipelines are deliberately rigid — there's no plug-in middleware
chain to configure. Every command goes through validate → authorize →
handle, in that order, every time. Every query goes through
authorize → cache lookup → handle → cache write, in that order.
Predictability beats flexibility for this layer.

---

## Quick start

```csharp
using FireflyFramework.Cqrs.Buses;
using FireflyFramework.Cqrs.DependencyInjection;

builder.Services.AddFireflyCqrs(typeof(Program).Assembly);
```

`AddFireflyCqrs(params Assembly[] assembliesToScan)` reflects every
supplied assembly for implementations of `ICommandHandler<,>` and
`IQueryHandler<,>` and registers them as **scoped** services.
`DefaultCommandBus`, `DefaultQueryBus`, and the fluent helpers are
wired automatically.

You can pass multiple assemblies if your handlers live in more than
one project:

```csharp
builder.Services.AddFireflyCqrs(
    typeof(PlaceOrderCommand).Assembly,        // Core project
    typeof(GetCustomerQuery).Assembly);         // a sibling project
```

---

## Authoring a command

A command is a record (or class) implementing `ICommand<TResult>`. It
optionally implements `ValidateAsync` and `AuthorizeAsync` — by
default both succeed.

```csharp
using FireflyFramework.Cqrs.Authorization;
using FireflyFramework.Cqrs.Commands;
using FireflyFramework.Cqrs.Validation;
using ExecutionContext = FireflyFramework.Cqrs.Context.ExecutionContext;

public sealed record CreateOrder(string Sku, int Quantity) : ICommand<Guid>
{
    public Task<ValidationResult> ValidateAsync(CancellationToken ct = default) =>
        Task.FromResult(Quantity > 0
            ? ValidationResult.Successful()
            : ValidationResult.Failed("Quantity", "must be > 0"));

    public Task<AuthorizationResult> AuthorizeAsync(ExecutionContext ctx, CancellationToken ct = default) =>
        Task.FromResult(ctx.UserId is not null
            ? AuthorizationResult.Allowed()
            : AuthorizationResult.Denied("UNAUTHENTICATED", "user id missing from context"));
}
```

The matching handler:

```csharp
public sealed class CreateOrderHandler(IOrderRepository repo)
    : ICommandHandler<CreateOrder, Guid>
{
    public async Task<Guid> HandleAsync(
        CreateOrder cmd, ExecutionContext ctx, CancellationToken ct = default)
    {
        var order = new Order(cmd.Sku, cmd.Quantity, ctx.UserId);
        await repo.SaveAsync(order, ct);
        return order.Id;
    }
}
```

Dispatch with the bus directly or with the fluent helper:

```csharp
// Direct.
var orderId = await commandBus.SendAsync(
    new CreateOrder("SKU-1", 2),
    new ExecutionContext { UserId = "alice" },
    ct);

// Fluent — sets common context fields without constructing ExecutionContext by hand.
var orderId = await commandBus
    .For(new CreateOrder("SKU-1", 2))
    .WithUser("alice")
    .WithTenant("acme")
    .WithCorrelation(correlationId)
    .ExecuteAsync(ct);
```

---

## Authoring a query (with caching)

```csharp
using FireflyFramework.Cqrs.Queries;

public sealed record GetOrder(Guid OrderId) : IQuery<OrderDto?>
{
    public bool      IsCacheable => true;
    public string?   CacheKey    => $"order:{OrderId}";
    public TimeSpan? CacheTtl    => TimeSpan.FromMinutes(5);
}

public sealed class GetOrderHandler(IOrderRepository repo)
    : IQueryHandler<GetOrder, OrderDto?>
{
    public Task<OrderDto?> HandleAsync(GetOrder q, ExecutionContext _, CancellationToken ct) =>
        repo.GetAsync(q.OrderId, ct);
}
```

The result is transparently cached under
`firefly:cqrs:query:order:{id}` when an `ICacheAdapter` is registered
(the framework's `Starter.Core` registers one by default).

Clear cache entries from anywhere that has the bus:

```csharp
await queryBus.ClearCacheAsync();              // every query result
await queryBus.ClearCacheAsync("order:");       // every key starting with "order:"
```

---

## Public surface

### Buses

| Type | Purpose |
|---|---|
| `ICommandBus` | `SendAsync<TResult>(ICommand<TResult>, ExecutionContext, CancellationToken)` |
| `IQueryBus` | `AskAsync<TResult>(IQuery<TResult>, ExecutionContext, CancellationToken)` plus `ClearCacheAsync(string? pattern = null)` |
| `DefaultCommandBus` | Validation → Authorization → Handler |
| `DefaultQueryBus` | Authorization → Cache lookup → Handler → Cache write |
| `CommandFluent<T>` | Fluent `For(cmd).WithUser(...).WithCorrelation(...).ExecuteAsync()` |
| `QueryFluent<T>` | Fluent `For(query).WithUser(...).ExecuteAsync()` |

### Result types

| Type | Key members |
|---|---|
| `ExecutionContext` | `UserId`, `TenantId`, `OrganizationId`, `SessionId`, `RequestId`, `Source`, `ClientIp`, `UserAgent`, `FeatureFlags`, `Properties`, `Roles` |
| `ValidationResult` | `IsValid`, `Failures` (each: `Field`, `Message`, `Code?`); helpers `Successful()`, `Failed(field, message)`, `Combine(...)` |
| `AuthorizationResult` | `IsAllowed`, `Errors` (each: `Code`, `Message`); helpers `Allowed()`, `Denied(code, message)` |

Buses throw `CqrsValidationException` (mapped to HTTP 400 by
`FireflyFramework.Web`) and `CqrsAuthorizationException` (mapped to
HTTP 403). Both inherit from `FireflyException`.

### Attributes

| Attribute | Purpose |
|---|---|
| `[CommandHandlerComponent]` | Class-level metadata on a command handler — timeout, retries, backoff, metric / tracing flags |
| `[QueryHandlerComponent]` | Class-level metadata on a query handler — timeout, cache TTL, metric / tracing flags |
| `[InvalidateCacheOn(typeof(EventX), Pattern = "users")]` | Class-level on a query handler. Tells `EventDrivenCacheInvalidator` to clear `firefly:cqrs:query:users:*` when an `EventX` arrives. |
| `[PublishDomainEvent("OrderCreated")]` | Method-level on a command handler. Used by EDA glue to publish a domain event on success. |

### Event-driven cache invalidation

```csharp
using FireflyFramework.Cqrs.Cache;

services.AddSingleton<EventDrivenCacheInvalidator>();

// At startup:
var invalidator = sp.GetRequiredService<EventDrivenCacheInvalidator>();
invalidator.RegisterFromAssemblies(new[] { typeof(GetOrder).Assembly });

// In an EDA listener:
public Task OnOrderCreated(OrderCreated evt, CancellationToken ct)
    => invalidator.OnEventAsync(evt, ct);
```

`RegisterFromAssemblies` walks every type tagged with
`[InvalidateCacheOn(...)]` and records the pair `(event type, cache
pattern)`. When an event arrives via `OnEventAsync`, every matching
pattern is cleared on the underlying `IQueryBus`.

This is the canonical "I changed something — invalidate the read
cache" wiring on the framework. It's eventually-consistent (the
event publish + handler latency window) but for the read paths that
typically use it (lists, dashboards), that's fine.

---

## Common patterns

### Composing validation rules

`ValidationResult.Combine(...)` merges multiple intermediate results
so you can validate field-by-field without nesting if/else:

```csharp
public Task<ValidationResult> ValidateAsync(CancellationToken ct = default)
{
    var results = new List<ValidationResult>();

    if (Quantity <= 0)
        results.Add(ValidationResult.Failed("Quantity", "must be > 0"));

    if (string.IsNullOrWhiteSpace(Sku))
        results.Add(ValidationResult.Failed("Sku", "is required"));
    else if (!Sku.StartsWith("SKU-"))
        results.Add(ValidationResult.Failed("Sku", "must start with 'SKU-'"));

    return Task.FromResult(ValidationResult.Combine(results));
}
```

### Tenant-scoped authorization

`ExecutionContext.TenantId` is propagated end-to-end. Use it for
multi-tenant isolation:

```csharp
public Task<AuthorizationResult> AuthorizeAsync(
    ExecutionContext ctx, CancellationToken ct = default)
{
    if (ctx.TenantId is null)
        return Task.FromResult(AuthorizationResult.Denied(
            "TENANT_REQUIRED", "tenant id missing from context"));

    if (ctx.TenantId != _resourceTenantId)
        return Task.FromResult(AuthorizationResult.Denied(
            "TENANT_MISMATCH", "caller's tenant differs from the resource"));

    return Task.FromResult(AuthorizationResult.Allowed());
}
```

### Sub-second cache for hot reads

The default cache adapter is in-memory with a per-key TTL. For
"frequent reads of the same value" patterns, use a low TTL (10-60
seconds) on `IQuery.CacheTtl`. The cache absorbs the burst; the
backend sees one request per TTL window.

```csharp
public sealed record GetActiveCustomerCount() : IQuery<int>
{
    public bool      IsCacheable => true;
    public string?   CacheKey    => "customer:active-count";
    public TimeSpan? CacheTtl    => TimeSpan.FromSeconds(15);
}
```

### Event-driven invalidation chain

When `CreateOrder` succeeds, the handler publishes an `OrderCreated`
event (via EDA). The query handler annotated with
`[InvalidateCacheOn(typeof(OrderCreated), Pattern = "order:")]`
receives the event through `EventDrivenCacheInvalidator` and clears
the matching cache prefix. Subsequent `GetOrder` queries hit the
database once and re-cache.

This pattern keeps read caches consistent without explicit
"invalidate after write" calls cluttering the command handler.

### Per-command timeouts

Annotate the handler:

```csharp
[CommandHandlerComponent(TimeoutMs = 5_000, Retries = 2, BackoffMs = 200)]
public sealed class CreateOrderHandler : ICommandHandler<CreateOrder, Guid> { … }
```

The bus reads the attribute and applies the timeout / retry policy
through Polly under the hood — handlers don't write the resilience
plumbing themselves.

---

## Pitfalls and gotchas

**Don't make `IsCacheable` a property of the *handler* — make it a
property of the *query record*.** The bus reads it off the query
instance because the same query class might be cacheable in some
flows and not others (e.g. when called with a `noCache=true` flag in
context). Putting it on the handler ties the policy to the
*implementation*, which is wrong.

**`CacheKey` is a deterministic string from the query's data.** Two
queries with different `OrderId` values must produce different
`CacheKey` strings, otherwise they hit each other's cache entries.
Include every input field in the key.

**Don't mutate `ExecutionContext` inside a handler.** Treat it as
read-only — the same instance may be visible to upstream and
downstream handlers in nested dispatches. If you need to add fields
for downstream code, construct a new context with `ctx with { … }`.

**`RegisterFromAssemblies` is a one-shot.** Calling it twice for the
same assembly registers patterns twice. Call it once at startup, not
per-request.

**The default cache adapter is in-memory.** If you run multiple
service instances behind a load balancer, they have *different*
caches. Switch to `FireflyFramework.Cache.Redis` for shared cache.

**Validation runs before authorization.** This is on purpose — a
caller who can't authorize shouldn't get error feedback about which
fields are wrong (it leaks information). If you have a multi-step
validation that itself reveals authorisation-relevant data, do that
inside the handler after authorisation succeeds.

**`SendAsync<TResult>` is generic.** Calling
`commandBus.SendAsync(myCommand, ctx)` without the type argument
forces the compiler to infer it from the command's `ICommand<TResult>`
implementation. If it can't (because the command parameter is typed
as `ICommand<>`), be explicit: `SendAsync<Guid>(myCommand, ctx)`.

---

## Internals (for the curious)

`DefaultCommandBus.SendAsync` does its work in three steps and
short-circuits on failure:

```
1. (cmd as IValidatable)?.ValidateAsync()  → throw CqrsValidationException
2. (cmd as IAuthorizable)?.AuthorizeAsync() → throw CqrsAuthorizationException
3. handler.HandleAsync(cmd, ctx, ct)
```

The cast pattern lets a command opt into validation / authorization by
implementing the interface, without forcing every command to override
both methods. Plain commands skip both steps and go straight to the
handler.

`DefaultQueryBus` checks `IsCacheable` *first*, then resolves the
handler. That avoids spinning up the handler's dependency graph just
to serve a cache hit. The cache key is `firefly:cqrs:query:` plus the
caller's `CacheKey`.

`EventDrivenCacheInvalidator` builds an `event-type → patterns`
multimap once at startup and looks up by exact type match. We
intentionally do not walk the inheritance chain — explicit
registration on the concrete event type prevents accidental
"every event clears every cache" mistakes when someone introduces a
new event base class.

`HandlerRegistry` is reflection at startup — once. The handler
lookups themselves go through `IServiceProvider`, which has its own
fast type-keyed cache. The reflection cost amortises to zero after
the first request.

---

## Dependencies

| Reference | Used for |
|---|---|
| `FireflyFramework.Kernel` (project) | `FireflyException` base for the bus exceptions |
| `FireflyFramework.Cache` (project) | Query result cache via `ICacheAdapter` |
| `Microsoft.Extensions.DependencyInjection` (BCL) | Handler registration, scoped lifetime |

The bus pipeline is a few hundred lines of code. There is no
mediator-library dependency (we deliberately did not pull in
MediatR-style abstractions because the pipeline is fixed and
prescribed by the framework).

---

## Java mapping

| .NET | Java |
|---|---|
| `ICommand<TResult>` | `Command<R>` |
| `IQuery<TResult>` | `Query<R>` |
| `ICommandHandler<TCommand, TResult>` | `CommandHandler<C, R>` |
| `IQueryHandler<TQuery, TResult>` | `QueryHandler<Q, R>` |
| `DefaultCommandBus` / `DefaultQueryBus` | `DefaultCommandBus` / `DefaultQueryBus` |
| `CommandFluent<T>` / `QueryFluent<T>` | `CommandBuilder` / `QueryBuilder` |
| `ExecutionContext` | `ExecutionContext` |
| `ValidationResult` / `AuthorizationResult` | `ValidationResult` / `AuthorizationResult` |
| `EventDrivenCacheInvalidator` | `EventDrivenCacheInvalidator` |
| `[CommandHandlerComponent]` / `[QueryHandlerComponent]` | `@CommandHandlerComponent` / `@QueryHandlerComponent` |
| `[InvalidateCacheOn]` | `@InvalidateCacheOn` |
| `[PublishDomainEvent]` | `@PublishDomainEvent` |
| `CqrsValidationException` / `CqrsAuthorizationException` | `CqrsValidationException` / `CqrsAuthorizationException` |

The wire shape is identical — a service running version *X* on
either runtime publishes the same domain events, accepts the same
commands, and produces the same query responses.

---

## See also

* [`FireflyFramework.Cache`](../FireflyFramework.Cache/README.md) — the cache adapter the query bus uses for result caching.
* [`FireflyFramework.Eda`](../FireflyFramework.Eda/README.md) — domain-event publishing (`[PublishDomainEvent]`) and cache-invalidation events (`[InvalidateCacheOn]`).
* [`FireflyFramework.EventSourcing`](../FireflyFramework.EventSourcing/README.md) — when commands write to event-sourced aggregates.
* [`FireflyFramework.Web`](../FireflyFramework.Web/README.md) — the RFC 7807 mapping for `CqrsValidationException` (400) and `CqrsAuthorizationException` (403).
