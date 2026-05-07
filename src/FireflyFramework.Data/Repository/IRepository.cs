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
