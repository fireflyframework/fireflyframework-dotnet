using System.Collections.Concurrent;

namespace FireflyFramework.Cqrs.Buses;

/// <summary>
/// Maps a request type (command or query) to a delegate that invokes its handler.
/// Mirrors Java <c>CommandHandlerRegistry</c> / <c>QueryHandlerRegistry</c>.
/// </summary>
public sealed class HandlerRegistry<TInvoker>
{
    private readonly ConcurrentDictionary<Type, TInvoker> _map = new();

    public void Register(Type requestType, TInvoker invoker)
    {
        if (!_map.TryAdd(requestType, invoker))
        {
            throw new InvalidOperationException(
                $"Handler for {requestType.FullName} is already registered");
        }
    }

    public bool Has(Type requestType) => _map.ContainsKey(requestType);

    public TInvoker Get(Type requestType) => _map.TryGetValue(requestType, out var i)
        ? i
        : throw new InvalidOperationException($"No handler registered for {requestType.FullName}");
}
