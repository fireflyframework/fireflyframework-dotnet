using FireflyFramework.Orchestration.Tcc;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FireflyFramework.Tests;

[Tcc("TransferTcc")]
public sealed class TransferCoordinator { }

[TccParticipant]
public sealed class DebitParticipant
{
    public List<string> Calls { get; } = new();

    [TryMethod] public Task<string> Try() { Calls.Add("debit-try"); return Task.FromResult("debit-reservation"); }
    [ConfirmMethod] public Task Confirm([FromTry] string reservation) { Calls.Add($"debit-confirm:{reservation}"); return Task.CompletedTask; }
    [CancelMethod] public Task Cancel([FromTry] string reservation) { Calls.Add($"debit-cancel:{reservation}"); return Task.CompletedTask; }
}

[TccParticipant]
public sealed class CreditParticipant
{
    public List<string> Calls { get; } = new();
    public bool ShouldFail { get; set; }

    [TryMethod] public Task<string> Try()
    {
        Calls.Add("credit-try");
        if (ShouldFail) throw new InvalidOperationException("insufficient liquidity");
        return Task.FromResult("credit-reservation");
    }

    [ConfirmMethod] public Task Confirm([FromTry] string reservation) { Calls.Add($"credit-confirm:{reservation}"); return Task.CompletedTask; }
    [CancelMethod] public Task Cancel([FromTry] string reservation) { Calls.Add($"credit-cancel:{reservation}"); return Task.CompletedTask; }
}

public class TccTests
{
    [Fact]
    public async Task All_participants_succeed_so_Confirm_runs_for_each()
    {
        var engine = new TccEngine(NullLogger<TccEngine>.Instance);
        var debit = new DebitParticipant();
        var credit = new CreditParticipant();
        var result = await engine.ExecuteAsync(new TransferCoordinator(), new object[] { debit, credit });

        result.Success.Should().BeTrue();
        debit.Calls.Should().Equal("debit-try", "debit-confirm:debit-reservation");
        credit.Calls.Should().Equal("credit-try", "credit-confirm:credit-reservation");
    }

    [Fact]
    public async Task Try_failure_triggers_Cancel_for_already_tried_participants()
    {
        var engine = new TccEngine(NullLogger<TccEngine>.Instance);
        var debit = new DebitParticipant();
        var credit = new CreditParticipant { ShouldFail = true };
        var result = await engine.ExecuteAsync(new TransferCoordinator(), new object[] { debit, credit });

        result.Success.Should().BeFalse();
        debit.Calls.Should().Equal("debit-try", "debit-cancel:debit-reservation");
        credit.Calls.Should().Equal("credit-try");
    }
}
