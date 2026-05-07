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
using FireflyFramework.Orchestration.Recovery;
using FireflyFramework.Orchestration.Workflow;
using Microsoft.AspNetCore.Mvc;

namespace FireflyFramework.Orchestration.Web;

/// <summary>
/// REST control plane for orchestration executions. Mirrors Java
/// <c>OrchestrationController</c>. Mounted at <c>/api/orchestration</c>; exposes:
///
/// <list type="bullet">
/// <item><c>GET /executions</c> — list executions, optionally filtered by status.</item>
/// <item><c>GET /executions/{id}</c> — fetch one execution by correlation id.</item>
/// <item><c>GET /executions/{id}/status</c> — current execution status.</item>
/// <item><c>GET /executions/{id}/steps</c> — every step's status + output.</item>
/// <item><c>GET /executions/{id}/variables</c> — every bound variable.</item>
/// <item><c>POST /recovery/cleanup</c> — reap completed executions older than a duration.</item>
/// </list>
///
/// <para>This controller is registered automatically when the consuming application calls
/// <c>app.MapControllers()</c> and references <c>FireflyFramework.Orchestration</c>.</para>
/// </summary>
[ApiController]
[Route("api/orchestration")]
public sealed class OrchestrationController : ControllerBase
{
    private readonly IExecutionPersistenceProvider _persistence;
    private readonly WorkflowQueryService _query;
    private readonly RecoveryService _recovery;

    public OrchestrationController(
        IExecutionPersistenceProvider persistence,
        WorkflowQueryService query,
        RecoveryService recovery)
    {
        _persistence = persistence;
        _query = query;
        _recovery = recovery;
    }

    [HttpGet("executions")]
    public async Task<IReadOnlyList<ExecutionSummary>> ListExecutions([FromQuery] ExecutionStatus? status, CancellationToken ct)
    {
        var source = status is null
            ? _persistence.FindInFlightAsync(ct)
            : _persistence.FindByStatusAsync(status.Value, ct);

        var list = new List<ExecutionSummary>();
        await foreach (var ctx in source.WithCancellation(ct).ConfigureAwait(false))
        {
            list.Add(Summary(ctx));
        }
        return list;
    }

    [HttpGet("executions/{id}")]
    public async Task<ActionResult<ExecutionSummary>> GetExecution(string id, CancellationToken ct)
    {
        var ctx = await _persistence.FindByIdAsync(id, ct).ConfigureAwait(false);
        return ctx is null ? NotFound() : Summary(ctx);
    }

    [HttpGet("executions/{id}/status")]
    public async Task<ActionResult<ExecutionStatus>> GetStatus(string id, CancellationToken ct)
    {
        var status = await _query.GetStatusAsync(id, ct).ConfigureAwait(false);
        return status is null ? NotFound() : status.Value;
    }

    [HttpGet("executions/{id}/steps")]
    public async Task<ActionResult<IReadOnlyDictionary<string, object?>>> GetSteps(string id, CancellationToken ct)
    {
        var results = await _query.GetStepResultsAsync(id, ct).ConfigureAwait(false);
        return results is null ? NotFound() : new ActionResult<IReadOnlyDictionary<string, object?>>(results);
    }

    [HttpGet("executions/{id}/variables")]
    public async Task<ActionResult<IReadOnlyDictionary<string, object?>>> GetVariables(string id, CancellationToken ct)
    {
        var vars = await _query.GetVariablesAsync(id, ct).ConfigureAwait(false);
        return vars is null ? NotFound() : new ActionResult<IReadOnlyDictionary<string, object?>>(vars);
    }

    [HttpPost("recovery/cleanup")]
    public async Task<ActionResult<CleanupResult>> Cleanup([FromQuery] int olderThanDays, CancellationToken ct)
    {
        if (olderThanDays <= 0) return BadRequest("olderThanDays must be > 0");
        var count = await _recovery.CleanupCompletedAsync(TimeSpan.FromDays(olderThanDays), ct).ConfigureAwait(false);
        return new CleanupResult(count);
    }

    private static ExecutionSummary Summary(OrchestrationExecutionContext ctx) => new(
        ctx.CorrelationId,
        ctx.Pattern,
        ctx.Status,
        ctx.StartedAt,
        ctx.CompletedAt,
        ctx.StepResults.Count);
}

/// <summary>Lightweight DTO for execution listings. Mirrors Java <c>ExecutionSummaryDto</c>.</summary>
public sealed record ExecutionSummary(
    string CorrelationId,
    ExecutionPattern Pattern,
    ExecutionStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    int StepCount);

/// <summary>Result of a recovery cleanup call.</summary>
public sealed record CleanupResult(int RemovedCount);
