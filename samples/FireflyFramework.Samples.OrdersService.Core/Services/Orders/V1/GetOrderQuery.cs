using FireflyFramework.Cqrs.Queries;
using FireflyFramework.Samples.OrdersService.Interfaces.Dtos.V1;

namespace FireflyFramework.Samples.OrdersService.Core.Services.Orders.V1;

public sealed record GetOrderQuery(Guid OrderId) : IQuery<OrderDto?>
{
    public bool IsCacheable => true;
    public string? CacheKey => $"order:{OrderId}";
    public TimeSpan? CacheTtl => TimeSpan.FromMinutes(5);
}
