using FireflyFramework.Samples.OrdersService.Models.Entities.V1;

namespace FireflyFramework.Samples.OrdersService.Models.Repositories;

public interface IOrderRepository
{
    Task<OrderEntity> SaveAsync(OrderEntity entity, CancellationToken ct = default);
    Task<OrderEntity?> GetAsync(Guid id, CancellationToken ct = default);
}
