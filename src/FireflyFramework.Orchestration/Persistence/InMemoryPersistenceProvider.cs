using System.Collections.Concurrent;
using FireflyFramework.Orchestration.Core;

namespace FireflyFramework.Orchestration.Persistence;

public sealed class InMemoryPersistenceProvider : IExecutionPersistenceProvider
{
    private readonly ConcurrentDictionary<string, OrchestrationExecutionContext> _store = new();

    public Task SaveAsync(OrchestrationExecutionContext state, CancellationToken ct = default)
    {
        _store[state.CorrelationId] = state;
        return Task.CompletedTask;
    }

    public Task<OrchestrationExecutionContext?> FindByIdAsync(string correlationId, CancellationToken ct = default) =>
        Task.FromResult(_store.TryGetValue(correlationId, out var s) ? s : null);

    public Task UpdateStatusAsync(string correlationId, ExecutionStatus status, CancellationToken ct = default)
    {
        if (_store.TryGetValue(correlationId, out var s))
        {
            s.Status = status;
        }

        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<OrchestrationExecutionContext> FindByPatternAsync(ExecutionPattern pattern, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        foreach (var s in _store.Values.Where(s => s.Pattern == pattern))
        {
            yield return s;
        }
    }

    public async IAsyncEnumerable<OrchestrationExecutionContext> FindByStatusAsync(ExecutionStatus status, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        foreach (var s in _store.Values.Where(s => s.Status == status))
        {
            yield return s;
        }
    }

    public async IAsyncEnumerable<OrchestrationExecutionContext> FindInFlightAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        foreach (var s in _store.Values.Where(s => s.Status is ExecutionStatus.Running or ExecutionStatus.Waiting or ExecutionStatus.Suspended))
        {
            yield return s;
        }
    }

    public Task<int> CleanupAsync(TimeSpan olderThan, CancellationToken ct = default)
    {
        var threshold = DateTimeOffset.UtcNow - olderThan;
        var removed = _store.Where(kvp => kvp.Value.CompletedAt is { } c && c < threshold)
            .Select(kvp => kvp.Key)
            .ToList();
        foreach (var key in removed)
        {
            _store.TryRemove(key, out _);
        }

        return Task.FromResult(removed.Count);
    }

    public Task<bool> IsHealthyAsync(CancellationToken ct = default) => Task.FromResult(true);
}
