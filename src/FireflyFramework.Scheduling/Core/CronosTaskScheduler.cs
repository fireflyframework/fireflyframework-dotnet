// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Collections.Concurrent;
using Cronos;
using Microsoft.Extensions.Logging;

namespace FireflyFramework.Scheduling.Core;

/// <summary>Single-process scheduler backed by <c>System.Threading.Timer</c> + Cronos parsing.</summary>
public sealed class CronosTaskScheduler : ITaskScheduler, IAsyncDisposable
{
    private readonly ILogger<CronosTaskScheduler> _logger;
    private readonly ConcurrentDictionary<string, ScheduledTask> _tasks = new(StringComparer.OrdinalIgnoreCase);

    public CronosTaskScheduler(ILogger<CronosTaskScheduler> logger) { _logger = logger; }

    public ScheduledTaskHandle ScheduleCron(string cron, Func<CancellationToken, Task> action, string? id = null, TimeZoneInfo? timeZone = null)
    {
        var expr = CronExpression.Parse(cron, cron.Split(' ').Length >= 6 ? CronFormat.IncludeSeconds : CronFormat.Standard);
        var taskId = id ?? Guid.NewGuid().ToString("N");
        var t = new ScheduledTask(taskId, $"cron:{cron}", action, timeZone ?? TimeZoneInfo.Utc) { CronExpression = expr };
        _tasks[taskId] = t;
        ScheduleNext(t);
        return new ScheduledTaskHandle(taskId, t.Description, t.NextRun);
    }

    public ScheduledTaskHandle ScheduleAtFixedRate(TimeSpan period, Func<CancellationToken, Task> action, TimeSpan initialDelay = default, string? id = null) =>
        ScheduleInterval($"fixedRate:{period}", period, action, initialDelay, id, isFixedDelay: false);

    public ScheduledTaskHandle ScheduleWithFixedDelay(TimeSpan delay, Func<CancellationToken, Task> action, TimeSpan initialDelay = default, string? id = null) =>
        ScheduleInterval($"fixedDelay:{delay}", delay, action, initialDelay, id, isFixedDelay: true);

    public bool Cancel(string id)
    {
        if (!_tasks.TryRemove(id, out var t)) return false;
        t.Cts.Cancel();
        t.Timer?.Dispose();
        return true;
    }

    public IReadOnlyList<ScheduledTaskHandle> GetAll() =>
        _tasks.Values.Select(t => new ScheduledTaskHandle(t.Id, t.Description, t.NextRun)).ToList();

    public async ValueTask DisposeAsync()
    {
        foreach (var t in _tasks.Values) { t.Cts.Cancel(); t.Timer?.Dispose(); }
        _tasks.Clear();
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private ScheduledTaskHandle ScheduleInterval(string desc, TimeSpan period, Func<CancellationToken, Task> action, TimeSpan initialDelay, string? id, bool isFixedDelay)
    {
        var taskId = id ?? Guid.NewGuid().ToString("N");
        var t = new ScheduledTask(taskId, desc, action, TimeZoneInfo.Utc) { Period = period, IsFixedDelay = isFixedDelay };
        _tasks[taskId] = t;
        t.NextRun = DateTimeOffset.UtcNow.Add(initialDelay == default ? period : initialDelay);
        ArmTimer(t, initialDelay == default ? period : initialDelay);
        return new ScheduledTaskHandle(taskId, t.Description, t.NextRun);
    }

    private void ScheduleNext(ScheduledTask t)
    {
        var next = t.CronExpression!.GetNextOccurrence(DateTimeOffset.UtcNow, t.TimeZone);
        if (next is null) return;
        t.NextRun = next.Value;
        var delay = next.Value - DateTimeOffset.UtcNow;
        if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
        ArmTimer(t, delay);
    }

    private void ArmTimer(ScheduledTask t, TimeSpan delay)
    {
        t.Timer?.Dispose();
        t.Timer = new Timer(async _ =>
        {
            if (t.Cts.IsCancellationRequested) return;
            try { await t.Action(t.Cts.Token).ConfigureAwait(false); }
            catch (Exception ex) { _logger.LogError(ex, "Scheduled task {Id} failed", t.Id); }

            if (t.CronExpression is not null) ScheduleNext(t);
            else if (!t.IsFixedDelay) ArmTimer(t, t.Period);
            else ArmTimer(t, t.Period);
        }, null, delay, Timeout.InfiniteTimeSpan);
    }

    private sealed class ScheduledTask
    {
        public ScheduledTask(string id, string desc, Func<CancellationToken, Task> action, TimeZoneInfo tz)
        { Id = id; Description = desc; Action = action; TimeZone = tz; }

        public string Id { get; }
        public string Description { get; }
        public Func<CancellationToken, Task> Action { get; }
        public TimeZoneInfo TimeZone { get; }
        public CronExpression? CronExpression { get; set; }
        public TimeSpan Period { get; set; }
        public bool IsFixedDelay { get; set; }
        public DateTimeOffset NextRun { get; set; } = DateTimeOffset.UtcNow;
        public Timer? Timer { get; set; }
        public CancellationTokenSource Cts { get; } = new();
    }
}

public sealed class TaskPoolExecutor : ITaskExecutor
{
    public void Execute(Action work) => Task.Run(work);
    public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct = default) => Task.Run(() => work(ct), ct);
}
