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

namespace FireflyFramework.Orchestration.Scheduling;

/// <summary>
/// Schedules orchestration tasks. Mirrors Java <c>OrchestrationScheduler</c>. Three trigger
/// styles are supported:
///
/// <list type="bullet">
/// <item><see cref="ScheduleAtFixedRate"/> — runs on a fixed wall-clock interval, regardless
///       of how long the previous invocation took. Mirrors
///       <c>ScheduledExecutorService.scheduleAtFixedRate</c>.</item>
/// <item><see cref="ScheduleWithFixedDelay"/> — waits a fixed delay <em>after</em> the previous
///       invocation completes. Mirrors <c>scheduleWithFixedDelay</c>.</item>
/// <item><see cref="ScheduleWithCron"/> — runs on a Cron schedule, optionally in a specific
///       <see cref="TimeZoneInfo"/>. Cron expressions follow the standard 5-field format
///       (<c>* * * * *</c>) plus optional 6-field with seconds.</item>
/// </list>
///
/// <para>The same <c>taskId</c> can be re-scheduled — the previous schedule for that id is
/// cancelled before the new one is registered, so callers can safely treat registration as
/// idempotent.</para>
/// </summary>
public interface IOrchestrationScheduler : IAsyncDisposable
{
    /// <summary>Runs <paramref name="task"/> every <paramref name="period"/>, starting after <paramref name="initialDelay"/>.</summary>
    void ScheduleAtFixedRate(string taskId, Func<CancellationToken, Task> task, TimeSpan initialDelay, TimeSpan period);

    /// <summary>Runs <paramref name="task"/> with <paramref name="delay"/> between the end of one run and the start of the next.</summary>
    void ScheduleWithFixedDelay(string taskId, Func<CancellationToken, Task> task, TimeSpan initialDelay, TimeSpan delay);

    /// <summary>Runs <paramref name="task"/> on the supplied Cron schedule, optionally pinned to a time zone.</summary>
    void ScheduleWithCron(string taskId, Func<CancellationToken, Task> task, string cronExpression, TimeZoneInfo? timeZone = null);

    /// <summary>Cancels the schedule for <paramref name="taskId"/>. Returns true iff a schedule was found.</summary>
    bool Cancel(string taskId);

    /// <summary>The number of currently-active schedules.</summary>
    int ActiveTaskCount { get; }
}
