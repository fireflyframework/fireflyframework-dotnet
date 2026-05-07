using FireflyFramework.Samples.OrdersService.Interfaces.Dtos.V1;

namespace FireflyFramework.Samples.OrdersService.Sdk;

public interface IOrdersServiceClient
{
    Task<Guid> PlaceOrderAsync(PlaceOrderRequest request, string? idempotencyKey = null, CancellationToken ct = default);
    Task<OrderDto?> GetOrderAsync(Guid id, CancellationToken ct = default);
}
