using FireflyFramework.Cqrs.Context;

namespace FireflyFramework.Cqrs.Queries;

/// <summary>Handler contract. Mirrors Java <c>QueryHandler&lt;Q, R&gt;</c>.</summary>
public interface IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query, ExecutionContext context, CancellationToken ct = default);
}
