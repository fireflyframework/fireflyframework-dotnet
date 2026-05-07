using System.Collections.Concurrent;
using FireflyFramework.Callbacks.Interfaces;

namespace FireflyFramework.Callbacks.Core;

/// <summary>
/// Persistence-agnostic store for callback configurations. Mirrors Java
/// <c>CallbackConfigurationRepository</c>.
/// </summary>
public interface ICallbackConfigurationStore
{
    Task<IReadOnlyList<CallbackConfigurationDto>> ListAsync(string? tenantId = null, CancellationToken ct = default);
    Task<CallbackConfigurationDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<CallbackConfigurationDto> CreateAsync(CallbackConfigurationDto dto, CancellationToken ct = default);
    Task<CallbackConfigurationDto?> UpdateAsync(Guid id, CallbackConfigurationDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CallbackConfigurationDto>> FindBySubscribedEventAsync(string eventType, string? tenantId = null, CancellationToken ct = default);
}

/// <summary>
/// Default in-process store. Replace with an EF Core implementation for production by
/// registering your own <see cref="ICallbackConfigurationStore"/> before the default one.
/// </summary>
public sealed class InMemoryCallbackConfigurationStore : ICallbackConfigurationStore
{
    private readonly ConcurrentDictionary<Guid, CallbackConfigurationDto> _store = new();

    public Task<IReadOnlyList<CallbackConfigurationDto>> ListAsync(string? tenantId = null, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CallbackConfigurationDto>>(_store.Values
            .Where(c => tenantId is null || c.TenantId == tenantId)
            .ToList());

    public Task<CallbackConfigurationDto?> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_store.TryGetValue(id, out var dto) ? dto : null);

    public Task<CallbackConfigurationDto> CreateAsync(CallbackConfigurationDto dto, CancellationToken ct = default)
    {
        var id = dto.Id ?? Guid.NewGuid();
        var stored = dto with { Id = id, CreatedAt = DateTimeOffset.UtcNow };
        _store[id] = stored;
        return Task.FromResult(stored);
    }

    public Task<CallbackConfigurationDto?> UpdateAsync(Guid id, CallbackConfigurationDto dto, CancellationToken ct = default)
    {
        if (!_store.ContainsKey(id)) return Task.FromResult<CallbackConfigurationDto?>(null);
        var updated = dto with { Id = id, UpdatedAt = DateTimeOffset.UtcNow };
        _store[id] = updated;
        return Task.FromResult<CallbackConfigurationDto?>(updated);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_store.TryRemove(id, out _));

    public Task<IReadOnlyList<CallbackConfigurationDto>> FindBySubscribedEventAsync(
        string eventType, string? tenantId = null, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CallbackConfigurationDto>>(_store.Values
            .Where(c => (tenantId is null || c.TenantId == tenantId) && c.Active && c.SubscribedEventTypes.Contains(eventType))
            .ToList());
}
