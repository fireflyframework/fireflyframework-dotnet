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
