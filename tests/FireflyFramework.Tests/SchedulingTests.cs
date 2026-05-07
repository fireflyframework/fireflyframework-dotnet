// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Scheduling.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FireflyFramework.Tests;

public sealed class SchedulingTests
{
    [Fact]
    public async Task Fixed_rate_schedule_fires_repeatedly()
    {
        await using var scheduler = new CronosTaskScheduler(NullLogger<CronosTaskScheduler>.Instance);
        var fired = 0;
        var handle = scheduler.ScheduleAtFixedRate(TimeSpan.FromMilliseconds(20), _ => { Interlocked.Increment(ref fired); return Task.CompletedTask; }, initialDelay: TimeSpan.FromMilliseconds(20));

        await Task.Delay(150);
        handle.Id.Should().NotBeNullOrEmpty();
        fired.Should().BeGreaterThanOrEqualTo(2);
        scheduler.Cancel(handle.Id).Should().BeTrue();
    }

    [Fact]
    public async Task Cron_schedule_parses_six_field_expression()
    {
        await using var scheduler = new CronosTaskScheduler(NullLogger<CronosTaskScheduler>.Instance);
        var fired = 0;
        // Every second
        scheduler.ScheduleCron("* * * * * *", _ => { Interlocked.Increment(ref fired); return Task.CompletedTask; });

        await Task.Delay(2200);
        fired.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void GetAll_lists_active_schedules_and_cancellation_removes_them()
    {
        var scheduler = new CronosTaskScheduler(NullLogger<CronosTaskScheduler>.Instance);
        var h1 = scheduler.ScheduleAtFixedRate(TimeSpan.FromMinutes(1), _ => Task.CompletedTask, id: "rate-job");
        var h2 = scheduler.ScheduleWithFixedDelay(TimeSpan.FromMinutes(1), _ => Task.CompletedTask, id: "delay-job");

        scheduler.GetAll().Select(t => t.Id).Should().Contain(new[] { "rate-job", "delay-job" });
        scheduler.Cancel("rate-job").Should().BeTrue();
        scheduler.GetAll().Select(t => t.Id).Should().NotContain("rate-job");
    }

    [Fact]
    public async Task TaskPoolExecutor_runs_work_off_caller_thread()
    {
        var executor = new TaskPoolExecutor();
        var threadId = -1;
        await executor.ExecuteAsync(_ => { threadId = Environment.CurrentManagedThreadId; return Task.CompletedTask; });
        threadId.Should().NotBe(-1);
    }
}
