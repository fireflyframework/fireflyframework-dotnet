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

using FireflyFramework.EventSourcing.Projection;
using FireflyFramework.EventSourcing.Store;
using FireflyFramework.EventSourcing.Upcasting;
using FluentAssertions;
using Xunit;

namespace FireflyFramework.Tests;

public class EventSourcingExtraTests
{
    private static StoredEventEnvelope NewEnvelope(string type, int version, string payload = "{}") => new(
        GlobalSequence: 1,
        AggregateId: Guid.NewGuid(),
        AggregateVersion: 0,
        AggregateType: "Aggregate",
        EventType: type,
        EventVersion: version,
        Payload: payload,
        Headers: null,
        Timestamp: DateTimeOffset.UtcNow,
        TenantId: null);

    private sealed class V1ToV2 : IEventUpcaster
    {
        public string EventType => "OrderCreated";
        public int FromVersion => 1;
        public int ToVersion => 2;
        public StoredEventEnvelope Upcast(StoredEventEnvelope e) =>
            e with { EventVersion = 2, Payload = e.Payload.Replace("\"v1\"", "\"v2\"") };
    }

    private sealed class V2ToV3 : IEventUpcaster
    {
        public string EventType => "OrderCreated";
        public int FromVersion => 2;
        public int ToVersion => 3;
        public StoredEventEnvelope Upcast(StoredEventEnvelope e) =>
            e with { EventVersion = 3, Payload = e.Payload.Replace("\"v2\"", "\"v3\"") };
    }

    [Fact]
    public void Upcaster_runs_chain_until_no_more_apply()
    {
        var svc = new EventUpcastingService(new IEventUpcaster[] { new V1ToV2(), new V2ToV3() });
        var input = NewEnvelope("OrderCreated", version: 1, payload: """{"flag":"v1"}""");

        var result = svc.Apply(input);

        result.EventVersion.Should().Be(3);
        result.Payload.Should().Contain("v3");
    }

    [Fact]
    public void Upcaster_returns_unchanged_when_no_match()
    {
        var svc = new EventUpcastingService(new[] { (IEventUpcaster)new V1ToV2() });
        var input = NewEnvelope("PaymentCaptured", 1);

        svc.Apply(input).Should().BeSameAs(input);
    }

    [Fact]
    public async Task CheckpointStore_persists_and_returns_progress()
    {
        var store = new InMemoryProjectionCheckpointStore();
        (await store.GetLastProcessedAsync("p1")).Should().Be(0);

        await store.SaveCheckpointAsync("p1", 42);
        (await store.GetLastProcessedAsync("p1")).Should().Be(42);
        (await store.GetLastProcessedAsync("other")).Should().Be(0);
    }

    [Fact]
    public async Task EventStore_streams_only_events_after_checkpoint()
    {
        var store = new InMemoryEventStore();
        var aggId = Guid.NewGuid();
        await store.AppendEventsAsync(aggId, "Account", new FireflyFramework.EventSourcing.Domain.IDomainEvent[]
        {
            new AccountOpened(aggId, DateTimeOffset.UtcNow, "alice", 100m),
            new MoneyDeposited(aggId, DateTimeOffset.UtcNow, 50m),
        }, expectedVersion: -1);

        var seen = new List<StoredEventEnvelope>();
        await foreach (var e in store.StreamAllEventsFromAsync(1)) seen.Add(e);

        seen.Should().HaveCount(1);
        seen[0].EventType.Should().Be("MoneyDeposited");
    }
}
