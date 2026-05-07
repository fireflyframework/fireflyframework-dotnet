# FireflyFramework.Cqrs

Async command and query buses with handler discovery, validation,
authorization, query result caching, fluent dispatch, and event-driven
cache invalidation. Mirrors `org.fireflyframework:firefly-common-cqrs`.

## Wiring

```csharp
using FireflyFramework.Cqrs.Buses;
using FireflyFramework.Cqrs.DependencyInjection;

builder.Services.AddFireflyCqrs(typeof(Program).Assembly);   // scans for handlers
```

`AddFireflyCqrs(params Assembly[])` reflects the supplied assemblies for
every implementation of `ICommandHandler<,>` and `IQueryHandler<,>` and
registers them as scoped services. `DefaultCommandBus` and
`DefaultQueryBus` are wired automatically.

## Authoring a command

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

public sealed class CreateOrderHandler(IOrderRepository repo) : ICommandHandler<CreateOrder, Guid>
{
    public async Task<Guid> HandleAsync(CreateOrder cmd, ExecutionContext ctx, CancellationToken ct = default)
    {
        var order = new Order(cmd.Sku, cmd.Quantity, ctx.UserId);
        await repo.SaveAsync(order, ct);
        return order.Id;
    }
}
```

Dispatch with `bus.SendAsync` or the fluent helper:

```csharp
var orderId = await commandBus
    .For(new CreateOrder("SKU-1", 2))
    .WithUser("alice")
    .WithCorrelation(correlationId)
    .ExecuteAsync(ct);
```

## Authoring a query (with caching)

```csharp
using FireflyFramework.Cqrs.Queries;

public sealed record GetOrder(Guid OrderId) : IQuery<OrderDto?>
{
    public bool       IsCacheable => true;
    public string?    CacheKey    => $"order:{OrderId}";
    public TimeSpan?  CacheTtl    => TimeSpan.FromMinutes(5);
}

public sealed class GetOrderHandler(IOrderRepository repo) : IQueryHandler<GetOrder, OrderDto?>
{
    public Task<OrderDto?> HandleAsync(GetOrder q, ExecutionContext _, CancellationToken ct) =>
        repo.GetAsync(q.OrderId, ct);
}
```

Result is transparently cached under `firefly:cqrs:query:order:{id}`
when an `ICacheAdapter` is registered. Clear with
`queryBus.ClearCacheAsync()` or `ClearCacheAsync("order:")`.

## Public surface

### Buses

| Type                       | Purpose                                                          |
|----------------------------|------------------------------------------------------------------|
| `ICommandBus`              | `SendAsync<TResult>(ICommand<TResult>, ExecutionContext)`        |
| `IQueryBus`                | `AskAsync<TResult>(IQuery<TResult>, ExecutionContext)` and `ClearCacheAsync` |
| `DefaultCommandBus`        | Validation → authorization → handler                             |
| `DefaultQueryBus`          | Authorization → cache lookup → handler → cache write             |
| `CommandFluent<T>`         | Fluent `For(cmd).WithUser().WithCorrelation().ExecuteAsync()`    |
| `QueryFluent<T>`            | Fluent `For(query).WithUser().WithCorrelation().ExecuteAsync()`  |
| `HandlerRegistry`          | Reflection-based registration helpers                            |

### Result types

| Type                  | Members                                                              |
|-----------------------|----------------------------------------------------------------------|
| `ExecutionContext`    | `UserId`, `TenantId`, `OrganizationId`, `SessionId`, `RequestId`, `Source`, `ClientIp`, `UserAgent`, `FeatureFlags`, `Properties`, `Roles` |
| `ValidationResult`    | `IsValid`, `Failures` (`Field`, `Message`, `Code?`)                  |
| `AuthorizationResult` | `IsAllowed`, `Errors` (`Code`, `Message`)                            |

Buses throw `CqrsValidationException` (HTTP 400) and
`CqrsAuthorizationException` (HTTP 403) which the Web layer translates
to RFC 7807 problem-detail responses.

### Annotations

| Attribute                              | Purpose                                                    |
|----------------------------------------|------------------------------------------------------------|
| `[CommandHandlerComponent]`            | Metadata: timeout, retries, backoff, metrics, tracing      |
| `[QueryHandlerComponent]`              | Metadata: timeout, cache, cache TTL, metrics, tracing      |
| `[InvalidateCacheOn(typeof(EventX))]`  | Clears cached query results when the named event arrives   |
| `[PublishDomainEvent("OrderCreated")]` | Marks a command method to publish a domain event on success |

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
`[InvalidateCacheOn(...)]` and registers its (event type, pattern)
pair. When an event arrives, the corresponding cache patterns are
cleared.

## Dependencies

| Reference                                | Used for                       |
|------------------------------------------|--------------------------------|
| `FireflyFramework.Kernel`                | Calendar version               |
| `FireflyFramework.Cache`                 | Query result cache             |
| `Microsoft.Extensions.DependencyInjection` | Handler registration         |

## Java mapping

| .NET                                   | Java                                                                  |
|----------------------------------------|-----------------------------------------------------------------------|
| `ICommand<TResult>`                    | `Command<R>`                                                          |
| `IQuery<TResult>`                      | `Query<R>`                                                            |
| `ICommandHandler<TCommand, TResult>`   | `CommandHandler<C, R>`                                                |
| `IQueryHandler<TQuery, TResult>`       | `QueryHandler<Q, R>`                                                  |
| `DefaultCommandBus` / `DefaultQueryBus` | `DefaultCommandBus` / `DefaultQueryBus`                              |
| `CommandFluent<T>` / `QueryFluent<T>`  | `CommandBuilder` / `QueryBuilder`                                     |
| `EventDrivenCacheInvalidator`          | `EventDrivenCacheInvalidator`                                         |
| `[InvalidateCacheOn]`                  | `@InvalidateCacheOn`                                                  |
| `[PublishDomainEvent]`                 | `@PublishDomainEvent`                                                 |
