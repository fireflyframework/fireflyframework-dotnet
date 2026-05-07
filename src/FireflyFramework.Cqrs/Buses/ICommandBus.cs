using FireflyFramework.Cqrs.Commands;
using FireflyFramework.Cqrs.Context;

namespace FireflyFramework.Cqrs.Buses;

public interface ICommandBus
{
    Task<TResult> SendAsync<TResult>(ICommand<TResult> command, ExecutionContext context, CancellationToken ct = default);

    Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default) =>
        SendAsync(command, ExecutionContext.Empty, ct);
}
