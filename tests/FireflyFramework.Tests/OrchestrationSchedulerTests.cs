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

using FireflyFramework.Orchestration.Scheduling;
using Xunit;

namespace FireflyFramework.Tests;

/// <summary>
/// Tests for <see cref="OrchestrationScheduler"/>. Pin the contracts for fixed-rate /
/// fixed-delay / cron triggers, idempotent re-registration, cancellation, and disposal.
/// </summary>
public sealed class OrchestrationSchedulerTests
{
    [Fact]
    public async Task ScheduleAtFixedRate_FiresMultipleTimes()
    {
        await using var scheduler = new OrchestrationScheduler();
        var fired = 0;
        var done = new TaskCompletionSource();

        scheduler.ScheduleAtFixedRate("burst",
            ct =>
            {
                if (Interlocked.Increment(ref fired) >= 3) done.TrySetResult();
                return Task.CompletedTask;
            },
            TimeSpan.Zero, TimeSpan.FromMilliseconds(20));

        await done.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(fired >= 3, $"expected ≥3 firings, got {fired}");
    }

    [Fact]
    public async Task ScheduleWithFixedDelay_WaitsAfterEachInvocation()
    {
        await using var scheduler = new OrchestrationScheduler();
        var fired = 0;
        var done = new TaskCompletionSource();

        scheduler.ScheduleWithFixedDelay("delayed",
            async ct =>
            {
                Interlocked.Increment(ref fired);
                if (fired >= 3) done.TrySetResult();
                await Task.Delay(5, ct);
            },
            TimeSpan.Zero, TimeSpan.FromMilliseconds(20));

        await done.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(fired >= 3);
    }

    [Fact]
    public async Task ScheduleWithCron_FiresOnSecondsExpression()
    {
        await using var scheduler = new OrchestrationScheduler();
        var fired = 0;
        var done = new TaskCompletionSource();

        // 6-field cron with seconds — every second.
        scheduler.ScheduleWithCron("every-second",
            ct =>
            {
                if (Interlocked.Increment(ref fired) >= 2) done.TrySetResult();
                return Task.CompletedTask;
            },
            "* * * * * *");

        await done.Task.WaitAsync(TimeSpan.FromSeconds(4));
        Assert.True(fired >= 2);
    }

    [Fact]
    public async Task Cancel_StopsTask_AndDecrementsActiveCount()
    {
        await using var scheduler = new OrchestrationScheduler();
        scheduler.ScheduleAtFixedRate("toCancel", _ => Task.CompletedTask, TimeSpan.Zero, TimeSpan.FromSeconds(10));
        Assert.Equal(1, scheduler.ActiveTaskCount);

        Assert.True(scheduler.Cancel("toCancel"));
        Assert.Equal(0, scheduler.ActiveTaskCount);
        Assert.False(scheduler.Cancel("toCancel"));

        await Task.CompletedTask;
    }

    [Fact]
    public async Task ReSchedulingSameId_ReplacesPreviousTask()
    {
        await using var scheduler = new OrchestrationScheduler();
        scheduler.ScheduleAtFixedRate("idempotent", _ => Task.CompletedTask, TimeSpan.Zero, TimeSpan.FromSeconds(10));
        scheduler.ScheduleAtFixedRate("idempotent", _ => Task.CompletedTask, TimeSpan.Zero, TimeSpan.FromSeconds(10));

        Assert.Equal(1, scheduler.ActiveTaskCount);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ExceptionsInTask_DoNotStopSchedule()
    {
        await using var scheduler = new OrchestrationScheduler();
        var fired = 0;
        var done = new TaskCompletionSource();

        scheduler.ScheduleAtFixedRate("flaky",
            ct =>
            {
                var n = Interlocked.Increment(ref fired);
                if (n == 1) throw new InvalidOperationException("first call always fails");
                if (n >= 3) done.TrySetResult();
                return Task.CompletedTask;
            },
            TimeSpan.Zero, TimeSpan.FromMilliseconds(20));

        await done.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(fired >= 3);
    }
}
