# FireflyFramework.Samples.OrdersService.Interfaces

The **public contract** for the Orders sample. Other services and
front-end clients depend only on this assembly — never on `.Models`,
`.Core`, or `.Web`.

## Contents

```
Dtos/V1/
  PlaceOrderRequest.cs
  OrderDto.cs
Enums/V1/
  OrderStatus.cs
```

## Conventions

- **Versioned namespaces** (`*.V1`) — additive evolution only; introduce
  `*.V2` for breaking changes and keep the old one alive until callers
  migrate.
- **Records, not classes** — DTOs are immutable transfer objects.
- **No project references** — depending on anything else turns this from
  a contract into an implementation.

## Java mapping

| .NET                                         | Java                                                |
|----------------------------------------------|-----------------------------------------------------|
| `Interfaces.Dtos.V1.PlaceOrderRequest`       | `interfaces.dtos.orders.v1.PlaceOrderRequestDto`    |
| `Interfaces.Dtos.V1.OrderDto`                | `interfaces.dtos.orders.v1.OrderDto`                |
| `Interfaces.Enums.V1.OrderStatus`            | `interfaces.enums.orders.v1.OrderStatus`            |
