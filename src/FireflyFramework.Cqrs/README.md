# FireflyFramework.Cqrs

Async command and query buses with handler discovery, validation, authorization, query result caching, correlation context. Mirrors `fireflyframework-cqrs`.

## Quick start

```csharp
builder.Services.AddFireflyCqrs(typeof(Program).Assembly); // scans assemblies for handlers

// Define a command
public sealed record CreateOrder(string Sku, int Quantity) : ICommand<Guid>
{
    public Task<ValidationResult> ValidateAsync(CancellationToken ct = default) =>
        Task.FromResult(Quantity > 0
            ? ValidationResult.Successful()
            : ValidationResult.Failed("Quantity", "Must be > 0"));
}

// Define its handler
public sealed class CreateOrderHandler(IOrderRepository repo) : ICommandHandler<CreateOrder, Guid>
{
    public async Task<Guid> HandleAsync(CreateOrder command, ExecutionContext context, CancellationToken ct = default)
    {
        var order = new Order(command.Sku, command.Quantity, context.UserId);
        await repo.SaveAsync(order, ct);
        return order.Id;
    }
}

// Use it
var bus = serviceProvider.GetRequiredService<ICommandBus>();
var orderId = await bus.SendAsync(new CreateOrder("SKU-1", 2), executionContext);
```

## What's inside

| Type | Purpose |
|---|---|
| `ICommand<TResult>` | Marker for write-side messages. Default `ValidateAsync` / `AuthorizeAsync` return success — override on the command to enforce rules. |
| `IQuery<TResult>` | Marker for read-side messages. Set `IsCacheable = true` and supply `CacheKey` / `CacheTtl` to enable query-bus caching. |
| `ICommandHandler<TCommand, TResult>` | Implement to handle a command. |
| `IQueryHandler<TQuery, TResult>` | Implement to handle a query. |
| `ICommandBus` / `IQueryBus` | Dispatchers. `DefaultCommandBus` and `DefaultQueryBus` are registered automatically. |
| `ExecutionContext` | Caller / request context (user, tenant, organization, session, request id, source, IP, UA, feature flags, custom properties, roles). |
| `ValidationResult` / `ValidationFailure` | Returned from `ICommand.ValidateAsync`. |
| `AuthorizationResult` / `AuthorizationError` | Returned from `ICommand.AuthorizeAsync` / `IQuery.AuthorizeAsync`. |
| `CqrsValidationException` / `CqrsAuthorizationException` | Thrown by buses when validation or authorization fails. |
| `[CommandHandlerComponent]` / `[QueryHandlerComponent]` | Optional metadata (timeout, retries, metrics, tracing, validation, priority). |
| `[InvalidateCacheOn(typeof(EventX))]` | Marks a query handler whose cache should be invalidated when an event arrives via EDA. |
| `[PublishDomainEvent("OrderCreated")]` | Marks a command method to publish a domain event after success. |

## Caching

When the registered `ICacheAdapter` (from `FireflyFramework.Cache`) is in DI, queries with `IsCacheable = true` are transparently cached under `firefly:cqrs:query:{CacheKey}`. Bypass via `IQueryBus.ClearCacheAsync(pattern)`.

## Authorization

Override `AuthorizeAsync` on the command/query and return `AuthorizationResult.Allowed()` or `AuthorizationResult.Denied("CODE", "message")`. The bus throws `CqrsAuthorizationException` on denial; the web layer translates that to HTTP 403.
