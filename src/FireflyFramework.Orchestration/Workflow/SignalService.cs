// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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
