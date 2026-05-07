# FireflyFramework.Data

Async data-access primitives — pagination, generic filtering, base
entity contract, repository abstraction. Mirrors
`org.fireflyframework:firefly-r2dbc` but built on EF Core 10 since EF is
the .NET standard for typed persistence.

The application supplies its own `DbContext`; this module supplies the
shared types every Firefly service needs around it.

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

Implement on every aggregate / entity:

```csharp
public sealed class Order : BaseEntity<Guid>
{
    public string Sku        { get; set; } = string.Empty;
    public int    Quantity   { get; set; }
    public decimal UnitPrice { get; set; }
}
```

### Pagination

| Type                       | Members                                                            |
|----------------------------|--------------------------------------------------------------------|
| `PaginationRequest`        | `PageNumber`, `PageSize`, `SortBy`, `SortDirection`, derived `Skip` |
| `PaginationResponse<T>`    | `Content`, `TotalElements`, `TotalPages`, `CurrentPage`, `PageSize` |
| `SortDirection`            | `Asc`, `Desc`                                                      |

```csharp
var page = await ordersRepository.FindAllAsync(
    new PaginationRequest { PageNumber = 0, PageSize = 50, SortBy = "CreatedAt", SortDirection = SortDirection.Desc },
    ct);
```

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
        From = new Dictionary<string, object?> { ["CreatedAt"] = DateTimeOffset.UtcNow.AddDays(-7) },
        To   = new Dictionary<string, object?> { ["CreatedAt"] = DateTimeOffset.UtcNow             },
    },
    Pagination = new PaginationRequest { PageNumber = 0, PageSize = 25 },
    Options    = new FilterOptions { CaseInsensitiveStrings = true },
};

// FilterRequest<T> is consumed by application-supplied query builders that
// translate it into Expression<Func<T, bool>> for IQueryable.
```

Special filter values:

- `FilterRequest<T>.NullValue` (`"__FIREFLY_NULL__"`) — match `IS NULL`.
- `FilterRequest<T>.NotNullValue` (`"__FIREFLY_NOT_NULL__"`) — match `IS NOT NULL`.

The static helpers `FilterRequest<T>.SetNullFilter` and
`SetNotNullFilter` write the sentinel values into a dictionary cleanly.

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

Implementations live in your service: typically a small `EfCoreRepository<T,
TId>` that wraps a `DbSet<T>`. The interface keeps the framework decoupled
from any concrete persistence library.

## Configuration

The module itself binds nothing — your application's `DbContext`
configuration drives everything. The common `Firefly:Data` shape used by
service starters is:

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

## Dependencies

| Reference                                   | Used for                       |
|---------------------------------------------|--------------------------------|
| `FireflyFramework.Kernel`                   | Calendar version               |
| `FireflyFramework.Utils`                    | `[FilterableId]`               |
| `Microsoft.EntityFrameworkCore`             | Repository implementations     |

## Java mapping

| .NET                        | Java                                      |
|-----------------------------|-------------------------------------------|
| `BaseEntity<TId>`           | `BaseEntity<ID>`                          |
| `PaginationRequest`         | `PaginationRequest`                       |
| `PaginationResponse<T>`     | `PaginationResponse<T>`                   |
| `FilterRequest<T>`          | `FilterRequest<T>`                        |
| `RangeFilter`               | `RangeFilter`                             |
| `IRepository<TEntity, TId>` | `BaseRepository<E, ID>`                   |
