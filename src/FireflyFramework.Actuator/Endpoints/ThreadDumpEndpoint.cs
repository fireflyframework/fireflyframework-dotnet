// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Diagnostics;
using FireflyFramework.Actuator.Core;

namespace FireflyFramework.Actuator.Endpoints;

public sealed class ThreadDumpEndpoint : IActuatorEndpoint
{
    public string Id => "threaddump";

    public Task<object?> InvokeAsync(IDictionary<string, string?> parameters, CancellationToken ct)
    {
        var p = Process.GetCurrentProcess();
        var threads = p.Threads.Cast<ProcessThread>().Select(t => new
        {
            id = t.Id,
            state = t.ThreadState.ToString(),
            startTime = TryGetStartTime(t),
            cpuTimeMs = TryGetCpuTime(t),
            priority = t.PriorityLevel.ToString(),
        });
        return Task.FromResult<object?>(new { threads });
    }

    private static DateTime? TryGetStartTime(ProcessThread t)
    { try { return t.StartTime; } catch { return null; } }

    private static double? TryGetCpuTime(ProcessThread t)
    { try { return t.TotalProcessorTime.TotalMilliseconds; } catch { return null; } }
}
