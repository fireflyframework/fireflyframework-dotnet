using FireflyFramework.Cqrs.Annotations;
using FireflyFramework.Cqrs.Buses;
using Microsoft.Extensions.Logging;

namespace FireflyFramework.Cqrs.Cache;

/// <summary>
/// Listens to domain events and clears query cache entries based on
/// <see cref="InvalidateCacheOnAttribute"/> declarations on query handlers.
/// Mirrors Java <c>EventDrivenCacheInvalidator</c>.
/// </summary>
public interface IEventDrivenCacheInvalidator
{
    Task OnEventAsync(object @event, CancellationToken ct = default);
    void Register(Type eventType, string? pattern = null);
}

public sealed class EventDrivenCacheInvalidator : IEventDrivenCacheInvalidator
{
    private readonly IQueryBus _queryBus;
    private readonly ILogger<EventDrivenCacheInvalidator> _log;
    private readonly Dictionary<Type, List<string?>> _registrations = new();

    public EventDrivenCacheInvalidator(IQueryBus queryBus, ILogger<EventDrivenCacheInvalidator> log)
    {
        _queryBus = queryBus;
        _log = log;
    }

    /// <summary>
    /// Scans the supplied assemblies for query handlers tagged with
    /// <see cref="InvalidateCacheOnAttribute"/> and registers each (eventType, pattern) pair.
    /// </summary>
    public void RegisterFromAssemblies(IEnumerable<System.Reflection.Assembly> assemblies)
    {
        foreach (var asm in assemblies)
        {
            foreach (var type in asm.GetTypes())
            {
                foreach (var attr in type.GetCustomAttributes(typeof(InvalidateCacheOnAttribute), inherit: false)
                    .Cast<InvalidateCacheOnAttribute>())
                {
                    Register(attr.EventType, attr.Pattern);
                }
            }
        }
    }

    public void Register(Type eventType, string? pattern = null)
    {
        if (!_registrations.TryGetValue(eventType, out var list))
        {
            list = new List<string?>();
            _registrations[eventType] = list;
        }
        list.Add(pattern);
    }

    public async Task OnEventAsync(object @event, CancellationToken ct = default)
    {
        var type = @event.GetType();
        if (!_registrations.TryGetValue(type, out var patterns))
        {
            return;
        }

        foreach (var pattern in patterns)
        {
            _log.LogDebug("Invalidating CQRS cache (event={Event}, pattern={Pattern})", type.Name, pattern ?? "<all>");
            await _queryBus.ClearCacheAsync(pattern, ct).ConfigureAwait(false);
        }
    }
}
