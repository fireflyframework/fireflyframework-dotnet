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

namespace FireflyFramework.Orchestration.Core;

/// <summary>Per-execution state. Mirrors Java <c>ExecutionContext</c>.</summary>
public sealed class OrchestrationExecutionContext
{
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString("N");
    public ExecutionPattern Pattern { get; init; }
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;
    public TccPhase? TccPhase { get; set; }
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }

    public ConcurrentDictionary<string, object?> Variables { get; } = new();
    public ConcurrentDictionary<string, string> Headers { get; } = new();
    public ConcurrentDictionary<string, StepResult> StepResults { get; } = new();
    public ConcurrentDictionary<string, string> IdempotencyKeys { get; } = new();

    public IReadOnlyList<string> CompletedSteps => StepResults
        .Where(p => p.Value.Status == StepStatus.Completed)
        .Select(p => p.Key)
        .ToList();
}

public sealed record StepResult(
    string StepId,
    StepStatus Status,
    object? Output,
    Exception? Error,
    TimeSpan Duration,
    int Attempts);
