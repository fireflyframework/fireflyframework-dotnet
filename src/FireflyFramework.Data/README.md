# FireflyFramework.Data

## Overview

`FireflyFramework.Data` is the **persistence-tier toolkit** for Firefly
services. It defines the shared types every service needs around its
own EF Core `DbContext`: a base entity contract, a pagination
request/response pair, a generic filter DSL with reflective query
construction, and a thin repository abstraction.

It mirrors `org.fireflyframework:firefly-r2dbc` from the Java line in
*scope* (the same set of building blocks), not in *runtime*: the Java
flavour ships R2DBC reactive primitives, while this .NET port targets
EF Core 10 because EF is the canonical ORM for .NET. Both expose the
same `FilterRequest<T>` DSL and `PaginationResponse<T>` shape so a
service that talks to both a Java upstream and a .NET upstream
deserialises the same JSON.

## Why a separate module?

The application owns its own `DbContext`. Anything that's specific to
the schema — entity configuration, migrations, indices, conversions —
lives there. What the *framework* owns is the cross-cutting machinery
that should not be reinvented in every service:

- A canonical `BaseEntity<TId>` contract so generic repositories,
  filters, and audit middleware can talk about "anything with an Id."
- A pagination request shape that's wire-compatible across the
  Java/.NET microservice mesh.
- A filter DSL that turns a flat dictionary of `(key, value)` pairs
  into a strongly-typed `IQueryable` predicate without forcing each
  service to write its own LINQ-tree builder.

This keeps `FireflyFramework.Data` deliberately narrow. There is no
opinion about which provider you use, no opinion about how you map
your aggregates, and no surprise in your migration history.

## Mental model

```
                 ┌──────────────────────────────┐
                 │   IRepository<TEntity, TId>  │  (port; you implement)
                 └────────────┬─────────────────┘
                              │
                       ┌──────┴──────┐
                       │ EF Core     │
                       │ Repository  │  (typically a small wrapper in your service)
                       └──────┬──────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
   ┌────▼─────┐         ┌─────▼────┐         ┌──────▼────┐
   │ Postgres │         │   MySQL  │         │ SqlServer │
   │ (Npgsql) │         │ (Pomelo) │         │  (MS)     │
   └──────────┘         └──────────┘         └───────────┘
        ▲
        │     ┌────────────────────────┐
        └─────│  GenericFilter<F,E,D>  │  (reflective IQueryable builder)
              │  uses FilterRequest<T> │
              └────────────────────────┘
```

The repository contract is small on purpose; you compose larger
aggregate-specific operations in your domain layer.

## Quick start

```csharp
public sealed class Order : BaseEntity<Guid>
{
    public string  Sku       { get; set; } = string.Empty;
    public int     Quantity  { get; set; }
    public decimal UnitPrice { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> opts)
    : DbContext(opts)
{
    public DbSet<Order> Orders => Set<Order>();
}

public sealed class OrderRepository(OrdersDbContext db) : IRepository<Order, Guid>
{
    public Task<Order?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);

    public async IAsyncEnumerable<Order> FindAllAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var o in db.Orders.AsAsyncEnumerable().WithCancellation(ct))
            yield return o;
    }

    public async Task<Order> SaveAsync(Order entity, CancellationToken ct = default)
    {
        db.Orders.Update(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var rows = await db.Orders.Where(o => o.Id == id).ExecuteDeleteAsync(ct);
        return rows > 0;
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) =>
        db.Orders.AnyAsync(o => o.Id == id, ct);

    public Task<long>  CountAsync(CancellationToken ct = default) =>
        db.Orders.LongCountAsync(ct);

    public async Task<PaginationResponse<Order>> FindAllAsync(PaginationRequest pagination, CancellationToken ct = default)
    {
        var total = await db.Orders.LongCountAsync(ct);
        var items = await db.Orders
            .OrderBy(o => o.CreatedAt)
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .ToListAsync(ct);
        return new PaginationResponse<Order>
        {
            Content       = items,
            TotalElements = total,
            TotalPages    = (int)Math.Ceiling((double)total / Math.Max(1, pagination.PageSize)),
            CurrentPage   = pagination.PageNumber,
            PageSize      = pagination.PageSize,
        };
    }
}
```

## Public surface

### Domain contracts

```csharp
namespace FireflyFramework.Data.Domain;

public interface IBaseEntity<TId>
{
    TId Id { get; }
}

public abstract class BaseEntity<TId> : IBaseEntity<TId>
{
    public TId Id { get; protected set; } = default!;
}
```

The `Id` setter is `protected` so callers can't mutate the identity
of an in-flight aggregate. Within the entity itself, you assign Id in
the constructor (or through a domain method that creates the
aggregate). For surrogate keys generated by the database, leave
`Id = default` and let EF Core fill it on insert.

### Pagination

| Type                       | Members                                                            |
|----------------------------|--------------------------------------------------------------------|
| `PaginationRequest`        | `PageNumber`, `PageSize`, `SortBy`, `SortDirection`, derived `Skip` |
| `PaginationResponse<T>`    | `Content`, `TotalElements`, `TotalPages`, `CurrentPage`, `PageSize` |
| `SortDirection`            | `Asc`, `Desc`                                                      |

```csharp
var page = await ordersRepository.FindAllAsync(
    new PaginationRequest
    {
        PageNumber    = 0,
        PageSize      = 50,
        SortBy        = "CreatedAt",
        SortDirection = SortDirection.Desc,
    },
    ct);

return new
{
    page.Content,
    page.TotalElements,
    page.TotalPages,
    page.CurrentPage,
    page.PageSize,
};
```

`PageNumber` is **zero-based** (the same as Spring Data's
`Pageable.getPageNumber()`). `Skip` is the derived offset
(`PageNumber * PageSize`). Both are clamped on read so a malicious or
buggy caller can't issue `PageSize=0` to force a divide-by-zero.

### Filtering DSL

```csharp
var request = new FilterRequest<Order>
{
    Filters = new Dictionary<string, object?>
    {
        ["Status"]    = "Placed",
        ["CustomerId"] = customerId,        // [FilterableId] required (see Utils)
    },
    RangeFilters = new RangeFilter
    {
        Ranges = new Dictionary<string, RangeFilter.Range>
        {
            ["CreatedAt"] = new(From: DateTimeOffset.UtcNow.AddDays(-7),
                                To:   DateTimeOffset.UtcNow),
            ["UnitPrice"] = new(From: 9.99m, To: 99.99m),
        },
    },
    Pagination = new PaginationRequest { PageNumber = 0, PageSize = 25 },
    Options    = new FilterOptions { CaseInsensitiveStrings = true },
};

var filter = new GenericFilter<Order, Order, OrderDto>(MapToDto);
var page = await filter.FilterAsync(
    db.Orders.AsQueryable(),
    request,
    countAsync:  (q, ct) => q.LongCountAsync(ct),
    toListAsync: (q, ct) => q.ToListAsync(ct),
    ct);
```

#### Filter semantics

| Value type                     | Translates to                                                  |
|--------------------------------|----------------------------------------------------------------|
| `null`                         | Skipped (no filter applied)                                    |
| `FilterRequest<T>.NullValue` (`"__FIREFLY_NULL__"`) | `WHERE col IS NULL`                       |
| `FilterRequest<T>.NotNullValue` (`"__FIREFLY_NOT_NULL__"`) | `WHERE col IS NOT NULL`            |
| `string` on string property    | `LIKE %value%` (case-folded if `CaseInsensitiveStrings`)       |
| `IEnumerable` on non-string    | `WHERE col IN (…)`                                             |
| any scalar                     | `WHERE col = value` (with type coercion)                       |

#### Range filter

`RangeFilter.Range(From, To)` is **inclusive on both bounds**:
`From <= col <= To`. Either bound may be `null` for an open-ended
range.

#### `[FilterableId]` opt-in

Properties whose names end in `Id` (e.g. `CustomerId`, `OrderId`) are
**not filterable** by default — this prevents callers from doing
broad scans by foreign key. Decorate the property with
`[FilterableId]` (from `FireflyFramework.Utils`) to allow it
explicitly:

```csharp
public sealed class Order : BaseEntity<Guid>
{
    [FilterableId]                              // explicitly filterable
    public Guid CustomerId { get; set; }

    public Guid PaymentMethodId { get; set; }   // not filterable
}
```

The `Id` property itself is always filterable (no attribute needed).

#### Sentinel helpers

```csharp
FilterRequest<Order>.SetNullFilter(filters,    "DeletedAt");
FilterRequest<Order>.SetNotNullFilter(filters, "PaidAt");
```

Setting a sentinel string into the dictionary is fine on its own;
the helpers exist for readability and to keep the magic string out
of caller code.

### Repository abstraction

```csharp
public interface IRepository<TEntity, TId> where TEntity : class
{
    Task<TEntity?> FindByIdAsync(TId id, CancellationToken ct = default);
    IAsyncEnumerable<TEntity> FindAllAsync(CancellationToken ct = default);
    Task<TEntity>  SaveAsync   (TEntity entity, CancellationToken ct = default);
    Task<bool>     DeleteAsync (TId id,         CancellationToken ct = default);
    Task<bool>     ExistsAsync (TId id,         CancellationToken ct = default);
    Task<long>     CountAsync  (CancellationToken ct = default);
    Task<PaginationResponse<TEntity>> FindAllAsync(PaginationRequest pagination, CancellationToken ct = default);
}
```

Implementations live in your service: typically a small
`EfCoreRepository<T, TId>` that wraps a `DbSet<T>` (see Quick start
above). The interface is deliberately small — you compose larger
aggregate-specific operations in your domain layer.

If you want a richer base class, derive from this interface and add
your own contract:

```csharp
public interface IOrderRepository : IRepository<Order, Guid>
{
    Task<IReadOnlyList<Order>> FindByCustomerAsync(Guid customerId, CancellationToken ct = default);
    Task<decimal>              SumRevenueByDayAsync(DateOnly day,  CancellationToken ct = default);
}
```

## Configuration

The module itself binds nothing — your application's `DbContext`
configuration drives everything. The common `Firefly:Data` shape used
by service starters is:

```json
{
  "Firefly": {
    "Data": {
      "Provider":         "Postgres",
      "ConnectionString": "Host=db;Port=5432;Database=orders;Username=app;Password=***",
      "MigrateOnStartup": true
    }
  }
}
```

| Provider value     | EF Core provider                                  |
|--------------------|---------------------------------------------------|
| `InMemory`         | `Microsoft.EntityFrameworkCore.InMemory`          |
| `Postgres`         | `Npgsql.EntityFrameworkCore.PostgreSQL`           |
| `MySql`            | `Pomelo.EntityFrameworkCore.MySql`                |
| `SqlServer`        | `Microsoft.EntityFrameworkCore.SqlServer`         |

The starter's `AddFireflyData<TContext>(...)` extension reads
`Provider` and wires the right `UseXxx(...)` builder; your service's
job is to `AddDbContext<OrdersDbContext>(...)` against the resolved
options.

## Common patterns

### Cursor pagination over time

For high-volume tables, offset pagination performs poorly past a few
thousand rows. Pair `PaginationRequest` with a "cursor" filter that
narrows by the previous page's last value:

```csharp
var request = new FilterRequest<Order>
{
    RangeFilters = new RangeFilter
    {
        Ranges = new() { ["CreatedAt"] = new(From: lastCursor, To: null) },
    },
    Pagination = new PaginationRequest
    {
        PageNumber = 0,                  // always 0 — cursor advances instead
        PageSize   = 50,
        SortBy     = "CreatedAt",
        SortDirection = SortDirection.Asc,
    },
};
```

The next call sets `lastCursor` to the last row's `CreatedAt`. The
filter DSL handles this without any custom code.

### Soft-delete pattern

Add a `DeletedAt` column and a global query filter; then expose a
`SetNullFilter`-based filter for the explicit "show me undeleted":

```csharp
modelBuilder.Entity<Order>().HasQueryFilter(o => o.DeletedAt == null);

// Operator query that bypasses the global filter
db.Orders.IgnoreQueryFilters().Where(o => o.DeletedAt != null);
```

### Tenant scoping via base class

```csharp
public abstract class TenantEntity<TId> : BaseEntity<TId>
{
    public string TenantId { get; protected set; } = default!;
}

modelBuilder.Entity<Order>()
    .HasQueryFilter(o => o.TenantId == _tenantContext.CurrentTenantId);
```

The query filter scopes every read; insert/update logic in
`SaveChangesAsync` populates `TenantId` on new rows.

### Migration on startup (dev only)

```csharp
if (options.MigrateOnStartup)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
    await db.Database.MigrateAsync(cancellationToken);
}
```

In production, run `dotnet ef database update` from the deployment
pipeline instead — `MigrateOnStartup` is convenient for local dev and
integration tests but couples application boot to schema migration.

## Pitfalls and gotchas

- **`PageNumber` is zero-based.** A first-time integrator will pass
  `PageNumber=1` and get the *second* page. Document at the API
  boundary which convention you expose to external callers.
- **`SortBy` is reflection-driven.** A typo'd property name silently
  falls back to "no sort." If you want to surface bad sort keys,
  validate `SortBy` against a whitelist before passing the request to
  the filter.
- **`*Id` properties are excluded from filters by default.** The
  filter builder skips foreign-key fields unless they carry
  `[FilterableId]`. Forgetting the attribute is a common source of
  "my filter doesn't seem to apply" — enable verbose logs on the
  generic filter when debugging.
- **String filters are LIKE, not equals.** A filter on
  `Sku = "ABC123"` translates to `WHERE sku LIKE '%ABC123%'`. Use the
  range-from-to or a sentinel to express equality precisely.
- **`GenericFilter` is reflective.** It handles routine cases well,
  but for performance-critical paths you'll do better with a
  hand-rolled LINQ expression. Profile before assuming the reflective
  path is fine for hot tables.
- **Range bounds are inclusive.** This may surprise you on
  date/time ranges where you expected an open upper bound. Subtract a
  tick at the call site if the half-open semantic matters.
- **Bulk operations bypass change tracking.** EF Core 10's
  `ExecuteDeleteAsync` and `ExecuteUpdateAsync` translate directly to
  SQL — they don't invoke `SaveChangesAsync` interceptors, audit
  fields, or domain events. If you depend on those, use the entity
  path (`Remove(...)` + `SaveChangesAsync`) instead.

## Internals (for the curious)

- `GenericFilter.ApplyFilter` builds an
  `Expression<Func<TEntity, bool>>` by reflecting on the property and
  composing `Expression.Equal` / `Expression.Call(Contains)`
  / `Expression.GreaterThanOrEqual` etc. EF Core's query translator
  receives the expression tree as if you wrote it inline — there's no
  client evaluation fallback.
- The cap on `PageSize` is intentionally absent from this module.
  Different services have different reasonable upper bounds (mobile
  app vs. analytics export). Add the validation in your service's
  request validation layer, not here.
- `FilterRequest<T>.NullValue` and `NotNullValue` are
  `__FIREFLY_NULL__` / `__FIREFLY_NOT_NULL__` — chosen to be
  sufficiently weird that they cannot collide with a legitimate user
  value. The `T` type parameter is purely a static-typing tag (the
  filter dictionary is keyed on string property names, not
  expressions); the type informs casing of the static helpers.
- `IRepository.FindAllAsync(...)` returns `IAsyncEnumerable` — the
  consumer can short-circuit (e.g. `.Take(100)`) without forcing the
  underlying provider to materialise more than needed.

## Dependencies

| Reference                                   | Used for                       |
|---------------------------------------------|--------------------------------|
| `FireflyFramework.Kernel`                   | Calendar version               |
| `FireflyFramework.Utils`                    | `[FilterableId]`               |
| `Microsoft.EntityFrameworkCore`             | Repository implementations     |

The starter packs add the EF Core *provider* (`Npgsql`, `Pomelo`, etc.);
this module itself depends only on the EF Core core package.

## Java mapping

| .NET                        | Java                                      |
|-----------------------------|-------------------------------------------|
| `BaseEntity<TId>`           | `BaseEntity<ID>`                          |
| `PaginationRequest`         | `PaginationRequest`                       |
| `PaginationResponse<T>`     | `PaginationResponse<T>`                   |
| `FilterRequest<T>`          | `FilterRequest<T>`                        |
| `RangeFilter`               | `RangeFilter`                             |
| `GenericFilter<F,E,D>`      | `FilterUtils.GenericFilter`               |
| `IRepository<TEntity, TId>` | `BaseRepository<E, ID>`                   |
| `[FilterableId]`            | `@FilterableId`                           |
