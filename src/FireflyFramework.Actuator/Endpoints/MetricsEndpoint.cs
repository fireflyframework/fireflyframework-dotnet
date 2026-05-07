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

public sealed class MetricsEndpoint : IActuatorEndpoint
{
    public string Id => "metrics";

    public Task<object?> InvokeAsync(IDictionary<string, string?> parameters, CancellationToken ct)
    {
        var p = Process.GetCurrentProcess();
        return Task.FromResult<object?>(new
        {
            process = new
            {
                cpuTimeMs = p.TotalProcessorTime.TotalMilliseconds,
                threads = p.Threads.Count,
                handles = p.HandleCount,
                workingSetBytes = p.WorkingSet64,
                privateBytes = p.PrivateMemorySize64,
            },
            gc = new
            {
                gen0 = GC.CollectionCount(0),
                gen1 = GC.CollectionCount(1),
                gen2 = GC.CollectionCount(2),
                totalAllocatedBytes = GC.GetTotalAllocatedBytes(),
                totalMemory = GC.GetTotalMemory(false),
            },
            uptime = DateTimeOffset.UtcNow - p.StartTime.ToUniversalTime(),
        });
    }
}
