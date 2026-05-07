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

using FireflyFramework.Data.Pagination;

namespace FireflyFramework.Data.Repository;

/// <summary>
/// Async repository contract. Mirrors Spring Data <c>ReactiveCrudRepository</c> with
/// idiomatic .NET shapes (Tasks, IAsyncEnumerable, optional cancellation).
/// </summary>
public interface IRepository<TEntity, TId> where TEntity : class
{
    Task<TEntity?> FindByIdAsync(TId id, CancellationToken ct = default);
    IAsyncEnumerable<TEntity> FindAllAsync(CancellationToken ct = default);
    Task<TEntity> SaveAsync(TEntity entity, CancellationToken ct = default);
    Task<bool> DeleteAsync(TId id, CancellationToken ct = default);
    Task<bool> ExistsAsync(TId id, CancellationToken ct = default);
    Task<long> CountAsync(CancellationToken ct = default);
    Task<PaginationResponse<TEntity>> FindAllAsync(PaginationRequest pagination, CancellationToken ct = default);
}
