// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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
