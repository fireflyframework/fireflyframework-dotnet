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
