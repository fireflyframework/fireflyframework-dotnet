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
using Cronos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FireflyFramework.Orchestration.Scheduling;

/// <summary>
/// Default <see cref="IOrchestrationScheduler"/> backed by <see cref="System.Threading.Timer"/>
/// loops and <see cref="Cronos.CronExpression"/> for cron parsing. Mirrors Java
/// <c>OrchestrationScheduler</c>; <c>Cronos</c> implements the same Quartz-flavoured cron
/// expression Spring's <c>CronExpression</c> understands (5-field minute-precision and
/// 6-field with seconds).
///
/// <para>Each schedule runs on the .NET thread pool. Exceptions thrown by user tasks are
/// caught, logged, and the schedule continues — a single bad invocation never tears down
/// the whole scheduler.</para>
/// </summary>
public sealed class OrchestrationScheduler : IOrchestrationScheduler
{
    private readonly ConcurrentDictionary<string, ScheduledTask> _tasks = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly ILogger<OrchestrationScheduler> _logger;
    private int _disposed;

    public OrchestrationScheduler(ILogger<OrchestrationScheduler>? logger = null)
    {
        _logger = logger ?? NullLogger<OrchestrationScheduler>.Instance;
    }

    public int ActiveTaskCount => _tasks.Count;

    public void ScheduleAtFixedRate(string taskId, Func<CancellationToken, Task> task, TimeSpan initialDelay, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(taskId);
        ArgumentNullException.ThrowIfNull(task);
        if (period <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(period), "must be positive");

        Cancel(taskId);
        var tcts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        var entry = new ScheduledTask(tcts, RunFixedRateLoop(taskId, task, initialDelay, period, tcts.Token));
        _tasks[taskId] = entry;
        _logger.LogInformation("[scheduler] scheduled '{TaskId}' at fixed rate {Period}", taskId, period);
    }

    public void ScheduleWithFixedDelay(string taskId, Func<CancellationToken, Task> task, TimeSpan initialDelay, TimeSpan delay)
    {
        ArgumentNullException.ThrowIfNull(taskId);
        ArgumentNullException.ThrowIfNull(task);
        if (delay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(delay), "must be non-negative");

        Cancel(taskId);
        var tcts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        var entry = new ScheduledTask(tcts, RunFixedDelayLoop(taskId, task, initialDelay, delay, tcts.Token));
        _tasks[taskId] = entry;
        _logger.LogInformation("[scheduler] scheduled '{TaskId}' with fixed delay {Delay}", taskId, delay);
    }

    public void ScheduleWithCron(string taskId, Func<CancellationToken, Task> task, string cronExpression, TimeZoneInfo? timeZone = null)
    {
        ArgumentNullException.ThrowIfNull(taskId);
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(cronExpression);

        var fields = cronExpression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var cron = fields.Length == 6 ? CronExpression.Parse(cronExpression, CronFormat.IncludeSeconds) : CronExpression.Parse(cronExpression);
        var zone = timeZone ?? TimeZoneInfo.Utc;

        Cancel(taskId);
        var tcts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        var entry = new ScheduledTask(tcts, RunCronLoop(taskId, task, cron, zone, tcts.Token));
        _tasks[taskId] = entry;
        _logger.LogInformation("[scheduler] scheduled '{TaskId}' with cron '{Expression}' in zone '{Zone}'", taskId, cronExpression, zone.Id);
    }

    public bool Cancel(string taskId)
    {
        if (!_tasks.TryRemove(taskId, out var task)) return false;
        task.Cts.Cancel();
        _logger.LogDebug("[scheduler] cancelled '{TaskId}'", taskId);
        return true;
    }

    private async Task RunFixedRateLoop(string taskId, Func<CancellationToken, Task> task, TimeSpan initialDelay, TimeSpan period, CancellationToken ct)
    {
        try
        {
            if (initialDelay > TimeSpan.Zero) await Task.Delay(initialDelay, ct).ConfigureAwait(false);
            while (!ct.IsCancellationRequested)
            {
                var nextTick = DateTimeOffset.UtcNow + period;
                await SafeInvokeAsync(taskId, task, ct).ConfigureAwait(false);
                var remaining = nextTick - DateTimeOffset.UtcNow;
                if (remaining > TimeSpan.Zero) await Task.Delay(remaining, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
    }

    private async Task RunFixedDelayLoop(string taskId, Func<CancellationToken, Task> task, TimeSpan initialDelay, TimeSpan delay, CancellationToken ct)
    {
        try
        {
            if (initialDelay > TimeSpan.Zero) await Task.Delay(initialDelay, ct).ConfigureAwait(false);
            while (!ct.IsCancellationRequested)
            {
                await SafeInvokeAsync(taskId, task, ct).ConfigureAwait(false);
                if (delay > TimeSpan.Zero) await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
    }

    private async Task RunCronLoop(string taskId, Func<CancellationToken, Task> task, CronExpression cron, TimeZoneInfo zone, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var nextRun = cron.GetNextOccurrence(DateTimeOffset.UtcNow, zone);
                if (nextRun is null)
                {
                    _logger.LogWarning("[scheduler] cron '{TaskId}' has no future occurrence; loop exiting", taskId);
                    return;
                }
                var delay = nextRun.Value - DateTimeOffset.UtcNow;
                if (delay > TimeSpan.Zero) await Task.Delay(delay, ct).ConfigureAwait(false);
                await SafeInvokeAsync(taskId, task, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
    }

    private async Task SafeInvokeAsync(string taskId, Func<CancellationToken, Task> task, CancellationToken ct)
    {
        try
        {
            await task(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[scheduler] task '{TaskId}' threw", taskId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _cts.Cancel();

        var loops = _tasks.Values.Select(t => t.Loop).ToArray();
        _tasks.Clear();
        try
        {
            await Task.WhenAll(loops).ConfigureAwait(false);
        }
        catch
        {
            // Loops swallow expected cancellations; anything else has been logged already.
        }
        _cts.Dispose();
    }

    private sealed record ScheduledTask(CancellationTokenSource Cts, Task Loop);
}
