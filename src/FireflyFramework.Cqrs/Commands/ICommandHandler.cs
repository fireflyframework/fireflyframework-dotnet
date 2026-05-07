using FireflyFramework.Cqrs.Context;

namespace FireflyFramework.Cqrs.Commands;

/// <summary>Handler contract. Mirrors Java <c>CommandHandler&lt;C, R&gt;</c>.</summary>
public interface ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult>
{
    Task<TResult> HandleAsync(TCommand command, ExecutionContext context, CancellationToken ct = default);
}
