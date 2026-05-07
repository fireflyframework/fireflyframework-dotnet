# FireflyFramework.Samples.OrdersService.Interfaces

## Overview

The **public contract** for the Orders sample. Other services and
front-end clients depend only on this assembly — never on `.Models`,
`.Core`, or `.Web`. It is a tiny dependency-free assembly that
defines the wire shapes the service speaks: DTOs, request/response
records, and enums.

This is the "narrow waist" of the service: changing
`PlaceOrderRequest` here is a wire-compatibility decision, with full
visibility to every consumer that imports it.

## Why a separate project?

A consumer service that wants to call Orders shouldn't have to take
a dependency on Orders' EF Core entities, repository implementations,
or business logic. It just wants to know the shape of the JSON body
to send. By isolating the contract:

- **Consumers stay light.** A 30 KB `Interfaces` assembly is the
  whole import.
- **Internal refactors don't break callers.** Renaming an entity
  property is invisible across the wire.
- **Versioning is explicit.** A `V1` namespace ships v1 DTOs; `V2`
  ships v2; both can coexist for the migration window.

## Contents

```
Dtos/V1/
  PlaceOrderRequest.cs       # POST body
  OrderDto.cs                # GET response + POST response
Enums/V1/
  OrderStatus.cs             # Pending / Confirmed / Shipped / Cancelled
```

```csharp
public sealed record PlaceOrderRequest(
    string  Sku,
    int     Quantity,
    decimal UnitPrice);

public sealed record OrderDto(
    Guid           Id,
    string         Sku,
    int            Quantity,
    decimal        UnitPrice,
    decimal        Total,
    OrderStatus    Status,
    DateTimeOffset CreatedAt);
```

The records are `sealed` and *immutable*. Mutation is explicit
through the C# `with` expression — no setter ambiguity, no
half-initialised DTOs.

## Conventions

- **Versioned namespaces** (`*.V1`) — additive evolution only.
  Introduce `*.V2` for breaking changes and keep the old one alive
  until callers migrate.
- **Records, not classes** — DTOs are immutable transfer objects.
  Records get value-equality, `Deconstruct`, and `with` for free.
- **No project references** — depending on anything else turns this
  from a contract into an implementation. The `csproj` lists no
  `<ProjectReference>` and the build enforces it.
- **`Dto` suffix optional** — when the type already lives under
  `Dtos/`, the suffix is redundant. We keep `OrderDto` because it
  surfaces in many call sites where the namespace isn't visible
  (e.g. controller signatures, Sdk return types) and the suffix
  helps readers.

## Common patterns

### Adding a new field (additive)

```csharp
public sealed record PlaceOrderRequest(
    string  Sku,
    int     Quantity,
    decimal UnitPrice,
    string? CustomerNote = null);   // optional, default null
```

System.Text.Json round-trips a missing field as `null`, so existing
callers continue to work without change.

### Renaming a field (breaking)

Don't. Add the new field, deprecate the old, and remove it in `V2`:

```csharp
namespace OrdersService.Interfaces.Dtos.V1;

public sealed record PlaceOrderRequest(
    string  Sku,
    int     Quantity,
    decimal UnitPrice,
    [property: Obsolete("use UnitPrice")] decimal? Price = null);
```

In `V2`, the field is gone:

```csharp
namespace OrdersService.Interfaces.Dtos.V2;

public sealed record PlaceOrderRequest(
    string  Sku,
    int     Quantity,
    decimal UnitPrice);
```

## Pitfalls and gotchas

- **Don't expose enum integers across versions.** If `OrderStatus`
  gains a new value, an old client that maps by ordinal may
  misinterpret the new value. Serialise enums as strings (the
  framework's default `JsonSerializerOptions` does this).
- **Don't add validation attributes here.** Validation belongs in
  `.Core` (the command's `ValidateAsync`). The DTO is the wire
  shape; what's *valid* is a domain rule.
- **Don't reference framework assemblies.** The `Interfaces` tier
  must remain dependency-free.

## Java mapping

| .NET                                         | Java                                                |
|----------------------------------------------|-----------------------------------------------------|
| `Interfaces.Dtos.V1.PlaceOrderRequest`       | `interfaces.dtos.orders.v1.PlaceOrderRequestDto`    |
| `Interfaces.Dtos.V1.OrderDto`                | `interfaces.dtos.orders.v1.OrderDto`                |
| `Interfaces.Enums.V1.OrderStatus`            | `interfaces.enums.orders.v1.OrderStatus`            |
