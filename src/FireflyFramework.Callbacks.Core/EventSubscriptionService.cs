using System.Collections.Concurrent;
using FireflyFramework.Callbacks.Interfaces;

namespace FireflyFramework.Callbacks.Core;

/// <summary>
/// Manages the (configurationId, eventType) subscription map. Mirrors Java
/// <c>EventSubscriptionService</c>.
/// </summary>
public interface IEventSubscriptionService
{
    Task SubscribeAsync(Guid configurationId, string eventType, CancellationToken ct = default);
    Task UnsubscribeAsync(Guid configurationId, string eventType, CancellationToken ct = default);
    Task<IReadOnlyList<EventSubscriptionDto>> ListAsync(Guid configurationId, CancellationToken ct = default);
    Task<IReadOnlyList<EventSubscriptionDto>> ListByEventTypeAsync(string eventType, CancellationToken ct = default);
}

public sealed class InMemoryEventSubscriptionService : IEventSubscriptionService
{
    private readonly ConcurrentDictionary<(Guid configId, string eventType), EventSubscriptionDto> _subs = new();

    public Task SubscribeAsync(Guid configurationId, string eventType, CancellationToken ct = default)
    {
        _subs[(configurationId, eventType)] = new EventSubscriptionDto(configurationId, eventType, IsActive: true);
        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(Guid configurationId, string eventType, CancellationToken ct = default)
    {
        _subs.TryRemove((configurationId, eventType), out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<EventSubscriptionDto>> ListAsync(Guid configurationId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<EventSubscriptionDto>>(_subs.Values
            .Where(s => s.ConfigurationId == configurationId)
            .ToList());

    public Task<IReadOnlyList<EventSubscriptionDto>> ListByEventTypeAsync(string eventType, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<EventSubscriptionDto>>(_subs.Values
            .Where(s => s.IsActive && s.EventType == eventType)
            .ToList());
}
