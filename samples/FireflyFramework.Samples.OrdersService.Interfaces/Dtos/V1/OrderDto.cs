using FireflyFramework.Samples.OrdersService.Interfaces.Enums.V1;

namespace FireflyFramework.Samples.OrdersService.Interfaces.Dtos.V1;

public sealed record OrderDto(Guid Id, string Sku, int Quantity, decimal Total, OrderStatus Status);
