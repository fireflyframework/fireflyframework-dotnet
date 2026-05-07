using System.Collections.Concurrent;
using FireflyFramework.Webhooks.Interfaces;
using Microsoft.Extensions.Logging;

namespace FireflyFramework.Webhooks.Core.Services;

public sealed record DeadLetterEntry(
    Guid Id,
    string Provider,
    string EventId,
    WebhookEventDto Event,
    string Reason,
    int Attempts,
    DateTimeOffset DeadLetteredAt);

public interface IDeadLetterQueueService
{
    Task PublishAsync(WebhookEventDto evt, string reason, int attempts, CancellationToken ct = default);
    IAsyncEnumerable<DeadLetterEntry> ListAsync(string? provider = null, int max = 100, CancellationToken ct = default);
    Task<int> RedriveAsync(Guid id, CancellationToken ct = default);
    Task<bool> RemoveAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// In-memory DLQ. Replace with a persistent store (Redis, EF Core) for production.
/// Mirrors Java <c>DeadLetterQueueService</c>.
/// </summary>
public sealed class InMemoryDeadLetterQueueService : IDeadLetterQueueService
{
    private readonly ConcurrentDictionary<Guid, DeadLetterEntry> _entries = new();
    private readonly ILogger<InMemoryDeadLetterQueueService> _log;

    public InMemoryDeadLetterQueueService(ILogger<InMemoryDeadLetterQueueService> log) => _log = log;

    public Task PublishAsync(WebhookEventDto evt, string reason, int attempts, CancellationToken ct = default)
    {
        var entry = new DeadLetterEntry(Guid.NewGuid(), evt.ProviderName, evt.EventId, evt, reason, attempts, DateTimeOffset.UtcNow);
        _entries[entry.Id] = entry;
        _log.LogWarning("Dead-lettered {EventId} from {Provider}: {Reason}", evt.EventId, evt.ProviderName, reason);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<DeadLetterEntry> ListAsync(
        string? provider = null,
        int max = 100,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        foreach (var e in _entries.Values
                     .Where(e => provider is null || e.Provider == provider)
                     .OrderByDescending(e => e.DeadLetteredAt)
                     .Take(max))
        {
            yield return e;
        }
    }

    public Task<int> RedriveAsync(Guid id, CancellationToken ct = default)
    {
        if (!_entries.TryRemove(id, out var entry))
        {
            return Task.FromResult(0);
        }

        // Caller is expected to re-enqueue the entry.Event into the processor;
        // returning 1 indicates the entry was dequeued.
        return Task.FromResult(1);
    }

    public Task<bool> RemoveAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_entries.TryRemove(id, out _));
}
