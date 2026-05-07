namespace FireflyFramework.Orchestration.Workflow;

/// <summary>
/// Workflow timer abstraction. Mirrors Java <c>TimerService</c>. The default
/// implementation simply uses <see cref="Task.Delay(TimeSpan, CancellationToken)"/>; a
/// production implementation can persist timers and survive process restarts.
/// </summary>
public class TimerService
{
    public virtual Task SleepAsync(TimeSpan duration, CancellationToken ct = default) =>
        Task.Delay(duration, ct);
}
