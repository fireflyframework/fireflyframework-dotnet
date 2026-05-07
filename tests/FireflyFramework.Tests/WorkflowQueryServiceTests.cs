using FireflyFramework.Orchestration.Core;
using FireflyFramework.Orchestration.Persistence;
using FireflyFramework.Orchestration.Workflow;
using Xunit;

namespace FireflyFramework.Tests;

/// <summary>
/// Tests for <see cref="WorkflowQueryService"/>. Pin the read-only contract: every getter
/// returns <c>null</c> for an unknown correlation id; otherwise it returns exactly the
/// state recorded in persistence.
/// </summary>
public sealed class WorkflowQueryServiceTests
{
    private static (WorkflowQueryService Query, OrchestrationExecutionContext Ctx, InMemoryPersistenceProvider Persistence) Build()
    {
        var ctx = new OrchestrationExecutionContext { Pattern = ExecutionPattern.Workflow, Status = ExecutionStatus.Running };
        ctx.StepResults["s1"] = new StepResult("s1", StepStatus.Completed, 42, null, TimeSpan.FromMilliseconds(5), 1);
        ctx.StepResults["s2"] = new StepResult("s2", StepStatus.Running, null, null, TimeSpan.Zero, 1);
        ctx.Variables["customerId"] = 12345;
        ctx.Variables["region"] = "eu-west-1";

        var persistence = new InMemoryPersistenceProvider();
        persistence.SaveAsync(ctx).Wait();
        return (new WorkflowQueryService(persistence), ctx, persistence);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsRecordedStatus()
    {
        var (q, ctx, _) = Build();
        Assert.Equal(ExecutionStatus.Running, await q.GetStatusAsync(ctx.CorrelationId));
        Assert.Null(await q.GetStatusAsync("missing"));
    }

    [Fact]
    public async Task GetCurrentStepsAsync_ReturnsRunningStepsOnly()
    {
        var (q, ctx, _) = Build();
        var steps = await q.GetCurrentStepsAsync(ctx.CorrelationId);

        Assert.NotNull(steps);
        Assert.Single(steps!);
        Assert.Equal("s2", steps[0]);
    }

    [Fact]
    public async Task GetStepStatusesAsync_MapsEveryStepToItsStatus()
    {
        var (q, ctx, _) = Build();
        var statuses = await q.GetStepStatusesAsync(ctx.CorrelationId);

        Assert.NotNull(statuses);
        Assert.Equal(StepStatus.Completed, statuses!["s1"]);
        Assert.Equal(StepStatus.Running, statuses!["s2"]);
    }

    [Fact]
    public async Task GetStepResultAsync_ReturnsOutput_OrNullForUnknownStep()
    {
        var (q, ctx, _) = Build();
        Assert.Equal(42, await q.GetStepResultAsync(ctx.CorrelationId, "s1"));
        Assert.Null(await q.GetStepResultAsync(ctx.CorrelationId, "nope"));
    }

    [Fact]
    public async Task GetVariableAsync_ReturnsBoundValue_OrNull()
    {
        var (q, ctx, _) = Build();
        Assert.Equal(12345, await q.GetVariableAsync(ctx.CorrelationId, "customerId"));
        Assert.Null(await q.GetVariableAsync(ctx.CorrelationId, "missingVar"));
    }

    [Fact]
    public async Task GetVariablesAsync_ReturnsAllVariables()
    {
        var (q, ctx, _) = Build();
        var vars = await q.GetVariablesAsync(ctx.CorrelationId);
        Assert.NotNull(vars);
        Assert.Equal(2, vars!.Count);
        Assert.Equal("eu-west-1", vars["region"]);
    }
}
