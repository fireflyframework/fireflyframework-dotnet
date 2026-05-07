# FireflyFramework.Samples.OrdersService.Sdk

A typed `HttpClient` so other services can call the Orders service in a
strongly-typed, idiomatic way without re-declaring DTOs.

## Contents

```
IOrdersServiceClient.cs           # the contract
OrdersServiceClient.cs            # implementation
OrdersServiceClientExtensions.cs  # AddOrdersServiceClient extension
```

## Wire-up

```csharp
services.AddOrdersServiceClient(new Uri("https://orders.example.com/"));
```

## Use

```csharp
public sealed class CheckoutHandler(IOrdersServiceClient orders)
{
    public Task<Guid> PlaceAsync(string sku, int qty, decimal unit) =>
        orders.PlaceOrderAsync(
            new PlaceOrderRequest(sku, qty, unit),
            idempotencyKey: Guid.NewGuid().ToString());
}
```

## Why a separate Sdk project

`Sdk` references **only** `Interfaces`, so a consumer pulls in DTOs and
nothing else — no `Models`, no EF Core, no business logic. This is the
same boundary the Java `*-sdk` Maven module enforces.
