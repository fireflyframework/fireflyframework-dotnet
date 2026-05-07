# FireflyFramework.Samples.OrdersService.Models

## Overview

The **persistence-tier** companion to `Interfaces`. It defines the
storage entities and the repository contract that `Core` depends on.
This is the only project that knows how data is stored — wire-format
evolution (in `Interfaces`) and schema evolution (here) move
independently.

The sample ships an `InMemoryOrderRepository` so the host runs
without a database. Production deployments swap it for an EF Core
implementation (see "Production checklist" below) without touching
`Core` or `Web`.

## Why entities are separate from DTOs

Two reasons keep entities (`OrderEntity`) separate from DTOs
(`OrderDto`):

1. **Different lifecycle.** Wire formats are versioned and
   *additive*; schemas are migrated and *evolving*. Coupling the
   two means every database column rename becomes a wire-breaking
   change.
2. **Different shape.** Entities carry persistence concerns —
   surrogate keys, optimistic-concurrency tokens, audit columns,
   navigation properties. None of these belong on the wire.

Mapping happens once, in `Core/Mappers/OrderMapper.cs`, and stays
trivially auditable.

## Mental model

```
   wire format                       storage format
   ┌──────────────┐                  ┌──────────────────┐
   │ OrderDto     │ ◄── mapper ────► │ OrderEntity      │
   │ (V1, V2…)    │                  │ (schema vN)      │
   └──────┬───────┘                  └────────┬─────────┘
          │                                   │
          │ via Interfaces                    │ via IOrderRepository
          ▼                                   ▼
   external callers                    EF Core / in-memory store
```

## Contents

```
Entities/V1/
  OrderEntity.cs           # the persistent record
Repositories/
  IOrderRepository.cs      # the contract Core depends on
  InMemoryOrderRepository.cs  # default impl — replace with EF Core in production
```

```csharp
public sealed class OrderEntity : BaseEntity<Guid>
{
    public string         Sku       { get; set; } = string.Empty;
    public int            Quantity  { get; set; }
    public decimal        UnitPrice { get; set; }
    public OrderStatus    Status    { get; set; } = OrderStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public interface IOrderRepository
{
    Task<OrderEntity?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<OrderEntity>  AddAsync     (OrderEntity entity, CancellationToken ct = default);
    Task<OrderEntity>  UpdateAsync  (OrderEntity entity, CancellationToken ct = default);
}
```

`OrderEntity` derives from `FireflyFramework.Data.BaseEntity<Guid>`
so it inherits the standard `Id` contract and plays nicely with the
generic filter DSL if you ever wire it.

## Production checklist

When you graduate from in-memory storage:

1. Replace `InMemoryOrderRepository` with an EF Core implementation:

   ```csharp
   public sealed class EfCoreOrderRepository(OrdersDbContext db) : IOrderRepository
   {
       public Task<OrderEntity?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
           db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);

       public async Task<OrderEntity> AddAsync(OrderEntity entity, CancellationToken ct = default)
       {
           db.Orders.Add(entity);
           await db.SaveChangesAsync(ct);
           return entity;
       }

       public async Task<OrderEntity> UpdateAsync(OrderEntity entity, CancellationToken ct = default)
       {
           db.Orders.Update(entity);
           await db.SaveChangesAsync(ct);
           return entity;
       }
   }
   ```

2. Register the `DbContext` via `AddFireflyData` from
   `FireflyFramework.Starter.Data` in `.Web`:

   ```csharp
   builder.Services.AddDbContext<OrdersDbContext>(opt =>
       opt.UseNpgsql(builder.Configuration["Firefly:Data:ConnectionString"]));
   builder.Services.AddScoped<IOrderRepository, EfCoreOrderRepository>();
   ```

3. Configure migrations under a `Migrations/` folder in this project:

   ```bash
   dotnet ef migrations add InitialCreate \
     -p samples/FireflyFramework.Samples.OrdersService.Models \
     -s samples/FireflyFramework.Samples.OrdersService.Web
   ```

## Pitfalls and gotchas

- **Don't expose `OrderEntity` over the wire.** Mapping it directly
  to JSON couples your schema to your callers. Always project
  through `OrderDto`.
- **The in-memory repository is *not* thread-safe across asserts.**
  It uses `ConcurrentDictionary` for storage but multi-step
  operations (read-modify-write) are not atomic. Use the EF Core
  variant under load.
- **`OrderEntity.Status` is a string-friendly enum.** Configure EF
  Core to convert via `HasConversion<string>()` so the column reads
  cleanly in psql.

## Java mapping

| .NET                                         | Java                                                |
|----------------------------------------------|-----------------------------------------------------|
| `Models.Entities.V1.OrderEntity`             | `models.entities.orders.v1.Order`                   |
| `Models.Repositories.IOrderRepository`       | `models.repositories.OrderRepository` (R2DBC)       |
