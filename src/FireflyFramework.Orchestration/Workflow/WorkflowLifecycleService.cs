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

using FireflyFramework.Orchestration.Core;
using FireflyFramework.Orchestration.Persistence;

namespace FireflyFramework.Orchestration.Workflow;

/// <summary>
/// Workflow-instance lifecycle operations on top of <see cref="IExecutionPersistenceProvider"/>.
/// Mirrors the lifecycle methods on Java <c>WorkflowEngine</c> — cancel / suspend / resume —
/// without coupling to a specific execution backend.
///
/// <para>Each method is a small state-machine guard: cancel only accepts in-flight states,
/// suspend only accepts <see cref="ExecutionStatus.Running"/>, resume only accepts
/// <see cref="ExecutionStatus.Suspended"/>. The engine itself is expected to honour these
/// status transitions at step boundaries (cancellation aborts the next step,
/// suspension parks the execution).</para>
/// </summary>
public sealed class WorkflowLifecycleService
{
    private readonly IExecutionPersistenceProvider _persistence;

    public WorkflowLifecycleService(IExecutionPersistenceProvider persistence)
    {
        _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
    }

    /// <summary>Marks the execution <see cref="ExecutionStatus.Cancelled"/> if it's still in-flight.</summary>
    public async Task<bool> CancelAsync(string correlationId, CancellationToken ct = default)
    {
        var ctx = await _persistence.FindByIdAsync(correlationId, ct).ConfigureAwait(false);
        if (ctx is null) return false;
        if (ctx.Status is ExecutionStatus.Completed or ExecutionStatus.Failed or ExecutionStatus.Cancelled or ExecutionStatus.Canceled or ExecutionStatus.TimedOut)
        {
            return false;
        }

        await _persistence.UpdateStatusAsync(correlationId, ExecutionStatus.Cancelled, ct).ConfigureAwait(false);
        return true;
    }

    /// <summary>Transitions <see cref="ExecutionStatus.Running"/> → <see cref="ExecutionStatus.Suspended"/>.</summary>
    public async Task<bool> SuspendAsync(string correlationId, CancellationToken ct = default)
    {
        var ctx = await _persistence.FindByIdAsync(correlationId, ct).ConfigureAwait(false);
        if (ctx is null || ctx.Status != ExecutionStatus.Running) return false;
        await _persistence.UpdateStatusAsync(correlationId, ExecutionStatus.Suspended, ct).ConfigureAwait(false);
        return true;
    }

    /// <summary>Transitions <see cref="ExecutionStatus.Suspended"/> → <see cref="ExecutionStatus.Running"/>.</summary>
    public async Task<bool> ResumeAsync(string correlationId, CancellationToken ct = default)
    {
        var ctx = await _persistence.FindByIdAsync(correlationId, ct).ConfigureAwait(false);
        if (ctx is null || ctx.Status != ExecutionStatus.Suspended) return false;
        await _persistence.UpdateStatusAsync(correlationId, ExecutionStatus.Running, ct).ConfigureAwait(false);
        return true;
    }

    /// <summary>Returns the execution context for one correlation id, or <c>null</c>.</summary>
    public Task<OrchestrationExecutionContext?> GetAsync(string correlationId, CancellationToken ct = default) =>
        _persistence.FindByIdAsync(correlationId, ct);
}
