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
using FireflyFramework.Orchestration.Workflow;
using Xunit;

namespace FireflyFramework.Tests;

/// <summary>
/// Tests for <see cref="WorkflowLifecycleService"/>. Pin the state-machine guards: cancel
/// only accepts in-flight states, suspend only accepts <see cref="ExecutionStatus.Running"/>,
/// resume only accepts <see cref="ExecutionStatus.Suspended"/>; unknown ids return false.
/// </summary>
public sealed class WorkflowLifecycleServiceTests
{
    private static (WorkflowLifecycleService Lifecycle, IExecutionPersistenceProvider Store, OrchestrationExecutionContext Ctx) Build(ExecutionStatus initialStatus)
    {
        var store = new InMemoryPersistenceProvider();
        var ctx = new OrchestrationExecutionContext { Pattern = ExecutionPattern.Workflow, Status = initialStatus };
        store.SaveAsync(ctx).Wait();
        return (new WorkflowLifecycleService(store), store, ctx);
    }

    [Fact]
    public async Task CancelAsync_InFlightExecution_ReturnsTrue_AndUpdatesStatus()
    {
        var (lifecycle, store, ctx) = Build(ExecutionStatus.Running);

        Assert.True(await lifecycle.CancelAsync(ctx.CorrelationId));
        Assert.Equal(ExecutionStatus.Cancelled, (await store.FindByIdAsync(ctx.CorrelationId))!.Status);
    }

    [Fact]
    public async Task CancelAsync_AlreadyCompleted_ReturnsFalse()
    {
        var (lifecycle, _, ctx) = Build(ExecutionStatus.Completed);
        Assert.False(await lifecycle.CancelAsync(ctx.CorrelationId));
    }

    [Fact]
    public async Task CancelAsync_UnknownId_ReturnsFalse()
    {
        var (lifecycle, _, _) = Build(ExecutionStatus.Running);
        Assert.False(await lifecycle.CancelAsync("does-not-exist"));
    }

    [Fact]
    public async Task SuspendAsync_OnlyAcceptsRunningState()
    {
        var (lifecycle, store, ctx) = Build(ExecutionStatus.Running);
        Assert.True(await lifecycle.SuspendAsync(ctx.CorrelationId));
        Assert.Equal(ExecutionStatus.Suspended, (await store.FindByIdAsync(ctx.CorrelationId))!.Status);

        // Suspending an already-suspended execution must fail.
        Assert.False(await lifecycle.SuspendAsync(ctx.CorrelationId));
    }

    [Fact]
    public async Task ResumeAsync_OnlyAcceptsSuspendedState()
    {
        var (lifecycle, store, ctx) = Build(ExecutionStatus.Suspended);

        Assert.True(await lifecycle.ResumeAsync(ctx.CorrelationId));
        Assert.Equal(ExecutionStatus.Running, (await store.FindByIdAsync(ctx.CorrelationId))!.Status);

        // Resuming a Running execution must fail (no transition).
        Assert.False(await lifecycle.ResumeAsync(ctx.CorrelationId));
    }

    [Fact]
    public async Task GetAsync_ReturnsContext_OrNull()
    {
        var (lifecycle, _, ctx) = Build(ExecutionStatus.Running);

        Assert.NotNull(await lifecycle.GetAsync(ctx.CorrelationId));
        Assert.Null(await lifecycle.GetAsync("does-not-exist"));
    }
}
