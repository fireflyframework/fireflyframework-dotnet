using FireflyFramework.Orchestration.Saga;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FireflyFramework.Tests;

[Saga("PaymentSaga")]
public sealed class PaymentSaga
{
    public List<string> Steps { get; } = new();

    [SagaStep("reserve", Compensate = nameof(ReleaseReservation))]
    public Task Reserve()
    {
        Steps.Add("reserved");
        return Task.CompletedTask;
    }

    [SagaStep("charge", Compensate = nameof(Refund), DependsOn = new[] { "reserve" })]
    public Task Charge()
    {
        Steps.Add("charged");
        return Task.CompletedTask;
    }

    public Task ReleaseReservation()
    {
        Steps.Add("released");
        return Task.CompletedTask;
    }

    public Task Refund()
    {
        Steps.Add("refunded");
        return Task.CompletedTask;
    }
}

[Saga("FailingSaga")]
public sealed class FailingSaga
{
    public List<string> Steps { get; } = new();

    [SagaStep("first", Compensate = nameof(UndoFirst))]
    public Task First()
    {
        Steps.Add("first");
        return Task.CompletedTask;
    }

    [SagaStep("second", DependsOn = new[] { "first" })]
    public Task Second() => throw new InvalidOperationException("kaboom");

    public Task UndoFirst()
    {
        Steps.Add("undo-first");
        return Task.CompletedTask;
    }
}

public class SagaTests
{
    [Fact]
    public async Task Saga_runs_steps_in_dependency_order()
    {
        var engine = new SagaEngine(NullLogger<SagaEngine>.Instance);
        var saga = new PaymentSaga();
        var result = await engine.ExecuteAsync(saga);
        result.Success.Should().BeTrue();
        saga.Steps.Should().Equal("reserved", "charged");
    }

    [Fact]
    public async Task Saga_compensates_completed_steps_on_failure()
    {
        var engine = new SagaEngine(NullLogger<SagaEngine>.Instance);
        var saga = new FailingSaga();
        var result = await engine.ExecuteAsync(saga);
        result.Success.Should().BeFalse();
        saga.Steps.Should().Equal("first", "undo-first");
    }
}
