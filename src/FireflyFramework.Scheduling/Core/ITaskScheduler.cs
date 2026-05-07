// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.Scheduling.Core;

public sealed record ScheduledTaskHandle(string Id, string Description, DateTimeOffset NextRun)
{
    public bool Cancelled { get; init; }
}

/// <summary>Spring <c>TaskScheduler</c> port.</summary>
public interface ITaskScheduler
{
    ScheduledTaskHandle ScheduleCron(string cron, Func<CancellationToken, Task> action, string? id = null, TimeZoneInfo? timeZone = null);
    ScheduledTaskHandle ScheduleAtFixedRate(TimeSpan period, Func<CancellationToken, Task> action, TimeSpan initialDelay = default, string? id = null);
    ScheduledTaskHandle ScheduleWithFixedDelay(TimeSpan delay, Func<CancellationToken, Task> action, TimeSpan initialDelay = default, string? id = null);
    bool Cancel(string id);
    IReadOnlyList<ScheduledTaskHandle> GetAll();
}

/// <summary>Spring <c>TaskExecutor</c> port (background work pool).</summary>
public interface ITaskExecutor
{
    void Execute(Action work);
    Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct = default);
}
