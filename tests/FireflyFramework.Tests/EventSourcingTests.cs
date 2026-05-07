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

using FireflyFramework.EventSourcing.Annotations;
using FireflyFramework.EventSourcing.Domain;
using FireflyFramework.EventSourcing.Store;
using FluentAssertions;
using Xunit;

namespace FireflyFramework.Tests;

[DomainEvent("AccountOpened")]
public sealed record AccountOpened(Guid AggregateId, DateTimeOffset Timestamp, string Owner, decimal InitialBalance) : AbstractDomainEvent(AggregateId, Timestamp);

[DomainEvent("MoneyDeposited")]
public sealed record MoneyDeposited(Guid AggregateId, DateTimeOffset Timestamp, decimal Amount) : AbstractDomainEvent(AggregateId, Timestamp);

public sealed class Account : AggregateRoot
{
    public string? Owner { get; private set; }
    public decimal Balance { get; private set; }

    public static Account Open(Guid id, string owner, decimal initial)
    {
        var account = new Account();
        account.ApplyChange(new AccountOpened(id, DateTimeOffset.UtcNow, owner, initial));
        return account;
    }

    public void Deposit(decimal amount) => ApplyChange(new MoneyDeposited(Id, DateTimeOffset.UtcNow, amount));

    private void On(AccountOpened e)
    {
        Id = e.AggregateId;
        Owner = e.Owner;
        Balance = e.InitialBalance;
    }

    private void On(MoneyDeposited e) => Balance += e.Amount;
}

public class EventSourcingTests
{
    [Fact]
    public async Task Account_replays_state_from_event_history()
    {
        var store = new InMemoryEventStore();
        var account = Account.Open(Guid.NewGuid(), "alice", 100m);
        account.Deposit(50);

        await store.AppendEventsAsync(account.Id, account.AggregateType, account.UncommittedChanges, expectedVersion: -1);
        var stream = await store.LoadEventStreamAsync(account.Id, account.AggregateType);
        stream.Events.Should().HaveCount(2);

        var version = await store.GetAggregateVersionAsync(account.Id, account.AggregateType);
        version.Should().Be(1);
    }

    [Fact]
    public async Task Append_with_wrong_expected_version_throws()
    {
        var store = new InMemoryEventStore();
        var id = Guid.NewGuid();
        await store.AppendEventsAsync(id, "Account", new[] { new AccountOpened(id, DateTimeOffset.UtcNow, "alice", 100) }, expectedVersion: -1);
        await FluentActions.Invoking(() => store.AppendEventsAsync(id, "Account",
                new[] { new MoneyDeposited(id, DateTimeOffset.UtcNow, 50) }, expectedVersion: 5))
            .Should().ThrowAsync<ConcurrencyException>();
    }
}
