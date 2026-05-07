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
