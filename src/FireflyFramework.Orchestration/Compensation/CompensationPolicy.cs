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

namespace FireflyFramework.Orchestration.Compensation;

/// <summary>
/// What the engine should do when a compensation step itself fails. Mirrors Java
/// <c>CompensationPolicy</c>.
/// </summary>
public enum CompensationFailureAction
{
    /// <summary>Halt the rollback chain and surface the error.</summary>
    Abort,
    /// <summary>Skip the failing step and continue rolling back earlier steps.</summary>
    Skip,
    /// <summary>Retry the failing step up to <see cref="CompensationPolicy.MaxRetries"/> times.</summary>
    Retry,
    /// <summary>Push to the dead-letter store and continue rolling back.</summary>
    DeadLetter,
}

public sealed record CompensationPolicy(
    CompensationFailureAction FailureAction = CompensationFailureAction.Abort,
    int MaxRetries = 3,
    TimeSpan? RetryDelay = null,
    bool ContinueOnFailure = false)
{
    public static CompensationPolicy Default { get; } = new();
    public static CompensationPolicy SkipOnFailure { get; } = new(CompensationFailureAction.Skip);
    public static CompensationPolicy RetryThenDeadLetter { get; } = new(CompensationFailureAction.Retry,
        MaxRetries: 3, RetryDelay: TimeSpan.FromSeconds(2), ContinueOnFailure: true);
}

public sealed record CompensationStepResult(
    string StepName,
    bool Success,
    int Attempts,
    string? Error,
    TimeSpan Duration);

public sealed record CompensationReport(
    string CorrelationId,
    bool AllStepsSucceeded,
    IReadOnlyList<CompensationStepResult> Steps,
    DateTimeOffset CompletedAt);
