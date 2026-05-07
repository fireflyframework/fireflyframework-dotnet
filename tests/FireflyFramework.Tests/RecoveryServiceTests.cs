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

using FireflyFramework.Orchestration.Core;
using FireflyFramework.Orchestration.Persistence;
using FireflyFramework.Orchestration.Recovery;
using Xunit;

namespace FireflyFramework.Tests;

/// <summary>
/// Tests for <see cref="RecoveryService"/>. Pin the contract: stale-execution detection
/// considers only in-flight statuses, the threshold is honoured, and cleanup propagates
/// counts and errors.
/// </summary>
public sealed class RecoveryServiceTests
{
    [Fact]
    public async Task FindStaleAsync_ReturnsOnlyInFlight_OlderThanThreshold()
    {
        var persistence = new InMemoryPersistenceProvider();
        var now = DateTimeOffset.UtcNow;
        var fresh = new OrchestrationExecutionContext { Pattern = ExecutionPattern.Saga, Status = ExecutionStatus.Running };
        SetStartedAt(fresh, now);
        var stale = new OrchestrationExecutionContext { Pattern = ExecutionPattern.Saga, Status = ExecutionStatus.Running };
        SetStartedAt(stale, now - TimeSpan.FromHours(1));
        var completed = new OrchestrationExecutionContext { Pattern = ExecutionPattern.Saga, Status = ExecutionStatus.Completed };
        SetStartedAt(completed, now - TimeSpan.FromHours(1));

        await persistence.SaveAsync(fresh);
        await persistence.SaveAsync(stale);
        await persistence.SaveAsync(completed);

        var recovery = new RecoveryService(persistence) { StaleThreshold = TimeSpan.FromMinutes(10) };

        var found = new List<OrchestrationExecutionContext>();
        await foreach (var s in recovery.FindStaleAsync()) found.Add(s);

        Assert.Single(found);
        Assert.Equal(stale.CorrelationId, found[0].CorrelationId);
    }

    [Fact]
    public async Task CleanupCompletedAsync_RemovesOldCompletedExecutions_AndReturnsCount()
    {
        var persistence = new InMemoryPersistenceProvider();
        var now = DateTimeOffset.UtcNow;
        var oldDone = new OrchestrationExecutionContext { Pattern = ExecutionPattern.Saga, Status = ExecutionStatus.Completed, CompletedAt = now - TimeSpan.FromDays(10) };
        var freshDone = new OrchestrationExecutionContext { Pattern = ExecutionPattern.Saga, Status = ExecutionStatus.Completed, CompletedAt = now - TimeSpan.FromHours(1) };
        await persistence.SaveAsync(oldDone);
        await persistence.SaveAsync(freshDone);

        var recovery = new RecoveryService(persistence);
        var removed = await recovery.CleanupCompletedAsync(TimeSpan.FromDays(5));

        Assert.Equal(1, removed);
        Assert.Null(await persistence.FindByIdAsync(oldDone.CorrelationId));
        Assert.NotNull(await persistence.FindByIdAsync(freshDone.CorrelationId));
    }

    [Fact]
    public async Task CleanupCompletedAsync_NonPositiveDuration_Throws()
    {
        var recovery = new RecoveryService(new InMemoryPersistenceProvider());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => recovery.CleanupCompletedAsync(TimeSpan.Zero));
    }

    [Fact]
    public void Constructor_RejectsNullPersistence() =>
        Assert.Throws<ArgumentNullException>(() => new RecoveryService(null!));

    /// <summary><see cref="OrchestrationExecutionContext.StartedAt"/> is init-only — use reflection in tests to set it.</summary>
    private static void SetStartedAt(OrchestrationExecutionContext ctx, DateTimeOffset value)
    {
        var prop = typeof(OrchestrationExecutionContext).GetProperty(nameof(OrchestrationExecutionContext.StartedAt))!;
        var backing = typeof(OrchestrationExecutionContext).GetField("<StartedAt>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        backing.SetValue(ctx, value);
    }
}
