using FireflyFramework.Cqrs.Authorization;
using FireflyFramework.Cqrs.Context;

namespace FireflyFramework.Cqrs.Queries;

/// <summary>
/// Marker for read-side messages. Mirrors Java <c>Query&lt;R&gt;</c>. Set
/// <see cref="IsCacheable"/> = true to opt into the QueryBus result cache.
/// </summary>
public interface IQuery<out TResult>
{
    bool IsCacheable => false;
    string? CacheKey => null;
    TimeSpan? CacheTtl => null;

    Task<AuthorizationResult> AuthorizeAsync(ExecutionContext context, CancellationToken ct = default) => Task.FromResult(AuthorizationResult.Allowed());
}
