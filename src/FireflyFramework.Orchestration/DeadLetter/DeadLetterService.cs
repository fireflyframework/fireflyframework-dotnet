using System.Collections.Concurrent;
using FireflyFramework.Orchestration.Core;
using Microsoft.Extensions.Logging;

namespace FireflyFramework.Orchestration.DeadLetter;

/// <summary>
/// Captures failed orchestration executions so an operator can replay or discard them.
/// Mirrors Java <c>DeadLetterService</c> + <c>DeadLetterStore</c>.
/// </summary>
public sealed record DeadLetterEntry(
    Guid Id,
    string CorrelationId,
    ExecutionPattern Pattern,
    string Reason,
    string? StackTrace,
    OrchestrationExecutionContext State,
    DateTimeOffset DeadLetteredAt);

public interface IDeadLetterStore
{
    Task PublishAsync(OrchestrationExecutionContext state, Exception cause, CancellationToken ct = default);
    Task<IReadOnlyList<DeadLetterEntry>> ListAsync(ExecutionPattern? pattern = null, int limit = 100, CancellationToken ct = default);
    Task<DeadLetterEntry?> GetAsync(Guid id, CancellationToken ct = default);
    Task<bool> RemoveAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Re-drives a dead-lettered execution back into the appropriate engine. Mirrors Java
/// <c>DeadLetterReplayService</c>.
/// </summary>
public interface IDeadLetterReplayService
{
    Task<bool> ReplayAsync(Guid deadLetterId, CancellationToken ct = default);
}

public sealed class InMemoryDeadLetterStore : IDeadLetterStore
{
    private readonly ConcurrentDictionary<Guid, DeadLetterEntry> _entries = new();
    private readonly ILogger<InMemoryDeadLetterStore> _log;

    public InMemoryDeadLetterStore(ILogger<InMemoryDeadLetterStore> log) => _log = log;

    public Task PublishAsync(OrchestrationExecutionContext state, Exception cause, CancellationToken ct = default)
    {
        var entry = new DeadLetterEntry(Guid.NewGuid(), state.CorrelationId, state.Pattern,
            cause.Message, cause.StackTrace, state, DateTimeOffset.UtcNow);
        _entries[entry.Id] = entry;
        _log.LogWarning(cause, "Dead-lettered orchestration {CorrelationId} ({Pattern}): {Reason}",
            state.CorrelationId, state.Pattern, cause.Message);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DeadLetterEntry>> ListAsync(ExecutionPattern? pattern = null, int limit = 100, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<DeadLetterEntry>>(_entries.Values
            .Where(e => pattern is null || e.Pattern == pattern)
            .OrderByDescending(e => e.DeadLetteredAt)
            .Take(limit)
            .ToList());

    public Task<DeadLetterEntry?> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_entries.TryGetValue(id, out var e) ? e : null);

    public Task<bool> RemoveAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_entries.TryRemove(id, out _));
}
