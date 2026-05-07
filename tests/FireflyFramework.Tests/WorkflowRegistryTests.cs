using FireflyFramework.Orchestration.Workflow;
using Xunit;

namespace FireflyFramework.Tests;

/// <summary>Tests for <see cref="WorkflowRegistry"/> + <see cref="WorkflowDescriptor"/>.</summary>
public sealed class WorkflowRegistryTests
{
    [Workflow("payment", Description = "Charges a card and emits a receipt.")]
    private sealed class PaymentWorkflow
    {
        [WorkflowStep("01-charge", Name = "Charge card")]
        public Task ChargeAsync() => Task.CompletedTask;

        [WorkflowStep("02-receipt", Name = "Emit receipt")]
        public Task ReceiptAsync() => Task.CompletedTask;
    }

    private sealed class NotAWorkflow { }

    [Fact]
    public void Register_StoresAttributeId()
    {
        var registry = new WorkflowRegistry();
        registry.Register<PaymentWorkflow>();

        Assert.NotNull(registry.Find("payment"));
        Assert.Same(typeof(PaymentWorkflow), registry.Find("payment"));
    }

    [Fact]
    public void Register_WithoutAttribute_Throws() =>
        Assert.Throws<InvalidOperationException>(() => new WorkflowRegistry().Register(typeof(NotAWorkflow)));

    [Fact]
    public void Find_UnknownId_ReturnsNull() =>
        Assert.Null(new WorkflowRegistry().Find("missing"));

    [Fact]
    public void Describe_PopulatesEveryStep()
    {
        var registry = new WorkflowRegistry();
        registry.Register<PaymentWorkflow>();

        var desc = registry.Describe("payment");
        Assert.NotNull(desc);
        Assert.Equal("payment", desc!.Id);
        Assert.Equal("Charges a card and emits a receipt.", desc.Description);
        Assert.Equal(2, desc.Steps.Count);
        Assert.Equal("01-charge", desc.Steps[0].Id);
        Assert.Equal("Charge card", desc.Steps[0].Name);
    }

    [Fact]
    public void RegisterFromAssembly_PicksUpEveryDecoratedClass()
    {
        var registry = new WorkflowRegistry();
        var count = registry.RegisterFromAssembly(typeof(WorkflowRegistryTests).Assembly);

        Assert.True(count >= 1);
        Assert.NotNull(registry.Find("payment"));
    }

    [Fact]
    public void GetAll_ReturnsRegisteredWorkflows()
    {
        var registry = new WorkflowRegistry();
        registry.Register<PaymentWorkflow>();

        var all = registry.GetAll();
        Assert.Single(all);
        Assert.Equal("payment", all[0].Id);
    }
}
