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
/// Read-only queries against running or completed workflow / saga / TCC executions.
/// Mirrors Java <c>WorkflowQueryService</c>. Three categories of query are supported:
///
/// <list type="bullet">
/// <item>Lifecycle — <see cref="GetStatusAsync"/>, <see cref="GetCurrentStepsAsync"/>.</item>
/// <item>Step state — <see cref="GetStepStatusesAsync"/>, <see cref="GetStepResultAsync"/>,
///       <see cref="GetStepResultsAsync"/>.</item>
/// <item>Variables — <see cref="GetVariablesAsync"/>, <see cref="GetVariableAsync"/>.</item>
/// </list>
///
/// <para>All queries return <c>null</c> when the correlation ID is unknown, distinguishing
/// "no such execution" from "execution exists but has no value for this key".</para>
/// </summary>
public sealed class WorkflowQueryService
{
    private readonly IExecutionPersistenceProvider _persistence;

    public WorkflowQueryService(IExecutionPersistenceProvider persistence)
    {
        _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
    }

    /// <summary>Returns the current <see cref="ExecutionStatus"/>, or <c>null</c> if unknown.</summary>
    public async Task<ExecutionStatus?> GetStatusAsync(string correlationId, CancellationToken ct = default)
    {
        var ctx = await _persistence.FindByIdAsync(correlationId, ct).ConfigureAwait(false);
        return ctx?.Status;
    }

    /// <summary>Returns step-id → <see cref="StepStatus"/>, or <c>null</c> if unknown.</summary>
    public async Task<IReadOnlyDictionary<string, StepStatus>?> GetStepStatusesAsync(string correlationId, CancellationToken ct = default)
    {
        var ctx = await _persistence.FindByIdAsync(correlationId, ct).ConfigureAwait(false);
        return ctx?.StepResults.ToDictionary(p => p.Key, p => p.Value.Status);
    }

    /// <summary>Returns the IDs of every step currently <see cref="StepStatus.Running"/>.</summary>
    public async Task<IReadOnlyList<string>?> GetCurrentStepsAsync(string correlationId, CancellationToken ct = default)
    {
        var ctx = await _persistence.FindByIdAsync(correlationId, ct).ConfigureAwait(false);
        return ctx?.StepResults
            .Where(p => p.Value.Status == StepStatus.Running)
            .Select(p => p.Key)
            .ToList();
    }

    /// <summary>Returns step-id → output (the <see cref="StepResult.Output"/>).</summary>
    public async Task<IReadOnlyDictionary<string, object?>?> GetStepResultsAsync(string correlationId, CancellationToken ct = default)
    {
        var ctx = await _persistence.FindByIdAsync(correlationId, ct).ConfigureAwait(false);
        return ctx?.StepResults.ToDictionary(p => p.Key, p => p.Value.Output);
    }

    /// <summary>Returns the output of one step, or <c>null</c> if the execution or step is unknown.</summary>
    public async Task<object?> GetStepResultAsync(string correlationId, string stepId, CancellationToken ct = default)
    {
        var ctx = await _persistence.FindByIdAsync(correlationId, ct).ConfigureAwait(false);
        return ctx?.StepResults.TryGetValue(stepId, out var result) == true ? result.Output : null;
    }

    /// <summary>Returns every variable bound to the execution.</summary>
    public async Task<IReadOnlyDictionary<string, object?>?> GetVariablesAsync(string correlationId, CancellationToken ct = default)
    {
        var ctx = await _persistence.FindByIdAsync(correlationId, ct).ConfigureAwait(false);
        return ctx is null ? null : new Dictionary<string, object?>(ctx.Variables);
    }

    /// <summary>Returns one variable by name, or <c>null</c> if the execution or variable is unknown.</summary>
    public async Task<object?> GetVariableAsync(string correlationId, string name, CancellationToken ct = default)
    {
        var ctx = await _persistence.FindByIdAsync(correlationId, ct).ConfigureAwait(false);
        return ctx is null ? null : ctx.Variables.TryGetValue(name, out var v) ? v : null;
    }
}
