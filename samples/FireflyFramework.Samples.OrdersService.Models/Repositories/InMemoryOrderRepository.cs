using System.Collections.Concurrent;
using FireflyFramework.Samples.OrdersService.Models.Entities.V1;

namespace FireflyFramework.Samples.OrdersService.Models.Repositories;

/// <summary>Trivial concurrent-dictionary store. Replace with EF Core in a real service.</summary>
public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly ConcurrentDictionary<Guid, OrderEntity> _store = new();

    public Task<OrderEntity> SaveAsync(OrderEntity entity, CancellationToken ct = default)
    {
        _store[entity.Id] = entity;
        return Task.FromResult(entity);
    }

    public Task<OrderEntity?> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_store.TryGetValue(id, out var entity) ? entity : null);
}
