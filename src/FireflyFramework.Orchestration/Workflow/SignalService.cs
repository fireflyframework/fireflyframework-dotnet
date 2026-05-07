using System.Collections.Concurrent;

namespace FireflyFramework.Orchestration.Workflow;

/// <summary>
/// In-process signal exchange used by <see cref="WorkflowEngine"/> to satisfy
/// <see cref="WaitForSignalAttribute"/>. A workflow blocks on a signal name; an
/// outside caller publishes the signal and the workflow resumes. Mirrors Java
/// <c>SignalService</c>.
/// </summary>
public sealed class SignalService
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<object?>> _waiters = new();

    public Task<object?> WaitAsync(string signalKey, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var tcs = _waiters.GetOrAdd(signalKey, _ => new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously));
        if (timeout is null)
        {
            return tcs.Task.WaitAsync(ct);
        }

        return tcs.Task.WaitAsync(timeout.Value, ct);
    }

    public bool Publish(string signalKey, object? payload = null)
    {
        if (!_waiters.TryRemove(signalKey, out var tcs))
        {
            return false;
        }

        return tcs.TrySetResult(payload);
    }
}

public sealed record SignalResult(bool Delivered, object? Payload);
