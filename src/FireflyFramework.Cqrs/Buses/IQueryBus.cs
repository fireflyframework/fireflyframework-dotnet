using FireflyFramework.Cqrs.Context;
using FireflyFramework.Cqrs.Queries;

namespace FireflyFramework.Cqrs.Buses;

public interface IQueryBus
{
    Task<TResult> AskAsync<TResult>(IQuery<TResult> query, ExecutionContext context, CancellationToken ct = default);

    Task<TResult> AskAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default) =>
        AskAsync(query, ExecutionContext.Empty, ct);

    Task ClearCacheAsync(string? pattern = null, CancellationToken ct = default);
}
