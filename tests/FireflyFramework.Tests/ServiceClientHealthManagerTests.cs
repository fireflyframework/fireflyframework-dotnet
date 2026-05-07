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

using FireflyFramework.Client.Health;
using Xunit;

namespace FireflyFramework.Tests;

/// <summary>
/// Tests for <see cref="ServiceClientHealthManager"/>. Pin the rollup contract: states
/// transition Healthy → Degraded → Unhealthy under consecutive failures, and a single
/// success resets the counter; the overall health rolls up the per-service states.
/// </summary>
public sealed class ServiceClientHealthManagerTests
{
    [Fact]
    public void NewService_RecordSuccess_IsHealthy()
    {
        var mgr = new ServiceClientHealthManager();
        mgr.RecordSuccess("users");

        var entry = mgr.Get("users");
        Assert.NotNull(entry);
        Assert.Equal(ServiceHealthState.Healthy, entry!.State);
        Assert.Equal(0, entry.ConsecutiveFailures);
    }

    [Fact]
    public void Failures_TransitionToDegraded_ThenUnhealthy()
    {
        var mgr = new ServiceClientHealthManager { DegradedThreshold = 1, FailureThreshold = 3 };

        mgr.RecordFailure("users", "timeout");
        Assert.Equal(ServiceHealthState.Degraded, mgr.Get("users")!.State);

        mgr.RecordFailure("users");
        Assert.Equal(ServiceHealthState.Degraded, mgr.Get("users")!.State);

        mgr.RecordFailure("users");
        Assert.Equal(ServiceHealthState.Unhealthy, mgr.Get("users")!.State);
        Assert.Equal(3, mgr.Get("users")!.ConsecutiveFailures);
    }

    [Fact]
    public void SingleSuccess_ResetsConsecutiveFailures()
    {
        var mgr = new ServiceClientHealthManager();
        mgr.RecordFailure("users");
        mgr.RecordFailure("users");
        mgr.RecordFailure("users");
        Assert.Equal(ServiceHealthState.Unhealthy, mgr.Get("users")!.State);

        mgr.RecordSuccess("users");
        Assert.Equal(ServiceHealthState.Healthy, mgr.Get("users")!.State);
        Assert.Equal(0, mgr.Get("users")!.ConsecutiveFailures);
    }

    [Fact]
    public void GetOverall_RollsUpAcrossServices()
    {
        var mgr = new ServiceClientHealthManager();
        mgr.RecordSuccess("a");
        mgr.RecordSuccess("b");
        mgr.RecordFailure("c"); // degraded

        var overall = mgr.GetOverall();
        Assert.Equal(3, overall.TotalServices);
        Assert.Equal(2, overall.HealthyServices);
        Assert.Equal(1, overall.DegradedServices);
        Assert.Equal(0, overall.UnhealthyServices);
        Assert.Equal(OverallHealthState.OutOfService, overall.State);

        mgr.RecordFailure("c");
        mgr.RecordFailure("c");
        Assert.Equal(OverallHealthState.Down, mgr.GetOverall().State);
    }
}
