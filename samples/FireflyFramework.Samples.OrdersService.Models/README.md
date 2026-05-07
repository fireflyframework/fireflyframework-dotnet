# FireflyFramework.Samples.OrdersService.Models

Persistence entities and the repository contract. This is the only place
that knows how data is stored.

## Contents

```
Entities/V1/
  OrderEntity.cs           # the persistent record
Repositories/
  IOrderRepository.cs      # the contract Core depends on
  InMemoryOrderRepository.cs  # default impl — replace with EF Core in production
```

## Why entities are separate from DTOs

DTOs (`OrderDto`) are the wire format. Entities (`OrderEntity`) are the
storage format. Mapping happens in `Core/Mappers/OrderMapper.cs`. This
keeps wire-format evolution and schema evolution independent.

## Production checklist

When you graduate from in-memory:

1. Replace `InMemoryOrderRepository` with an EF Core implementation that
   inherits from `FireflyFramework.Data.Repositories.RepositoryBase<TEntity, TKey>`.
2. Register the `DbContext` via `AddFireflyData` from
   `FireflyFramework.Starter.Data` in `.Web`.
3. Configure migrations under a `Migrations/` folder in this project.

## Java mapping

| .NET                                         | Java                                                |
|----------------------------------------------|-----------------------------------------------------|
| `Models.Entities.V1.OrderEntity`             | `models.entities.orders.v1.Order`                   |
| `Models.Repositories.IOrderRepository`       | `models.repositories.OrderRepository` (R2DBC)       |
