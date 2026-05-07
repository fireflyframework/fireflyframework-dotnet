using FireflyFramework.Samples.OrdersService.Interfaces.Enums.V1;

namespace FireflyFramework.Samples.OrdersService.Models.Entities.V1;

public sealed record OrderEntity(
    Guid Id,
    string Sku,
    int Quantity,
    decimal UnitPrice,
    decimal Total,
    OrderStatus Status,
    DateTimeOffset CreatedAt);
