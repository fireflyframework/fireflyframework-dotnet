# FireflyFramework.Scheduling

Cron / fixed-rate / fixed-delay scheduling — Spring `@Scheduled` port for
the .NET stack.

## What it provides

| Concept | .NET form |
|---|---|
| `@Scheduled(cron=...)` | `[Scheduled(Cron = "0 */5 * * * *")]` |
| `@Scheduled(fixedRate=...)` | `[Scheduled(FixedRate = "00:01:00")]` |
| `@Scheduled(fixedDelay=...)` | `[Scheduled(FixedDelay = "00:00:30")]` |
| `TaskScheduler` | `ITaskScheduler` (Cronos-backed default) |
| `TaskExecutor` | `ITaskExecutor` (TPL pool default) |
| `@Async` | `[Async]` (advisory marker) |

## Quick start

```csharp
services.AddFireflyScheduling()
        .AddScheduledHost<MaintenanceJobs>();

public sealed class MaintenanceJobs : IScheduledTaskHost
{
    [Scheduled(Cron = "0 0 3 * * *", Zone = "UTC")]
    public Task NightlyCleanupAsync(CancellationToken ct) { ... }

    [Scheduled(FixedRate = "00:01:00", InitialDelay = "00:00:10")]
    public Task HeartbeatAsync(CancellationToken ct) { ... }
}
```

The hosted service discovers `IScheduledTaskHost` registrations on
startup and arms each `[Scheduled]` method against `ITaskScheduler`.
