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
using FireflyFramework.EventSourcing.Snapshot;
using FireflyFramework.EventSourcing.Store;
using FireflyFramework.EventSourcing.Store.EfCore;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory.Infrastructure.Internal;
using Xunit;

namespace FireflyFramework.Tests;

[DomainEvent("CartCreated")]
public sealed record CartCreated(Guid AggregateId, DateTimeOffset Timestamp, string Owner) : AbstractDomainEvent(AggregateId, Timestamp);

[DomainEvent("ItemAddedToCart")]
public sealed record ItemAddedToCart(Guid AggregateId, DateTimeOffset Timestamp, string Sku, int Quantity) : AbstractDomainEvent(AggregateId, Timestamp);

public class EfCoreEventStoreTests
{
    private static IDbContextFactory<EventStoreDbContext> InMemoryFactory(string dbName)
    {
        var options = new DbContextOptionsBuilder<EventStoreDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new InMemoryFactoryImpl(options);
    }

    [Fact]
    public async Task EfCoreEventStore_appends_and_loads_events()
    {
        var factory = InMemoryFactory(nameof(EfCoreEventStore_appends_and_loads_events));
        var store = new EfCoreEventStore(factory, new[] { typeof(CartCreated), typeof(ItemAddedToCart) });
        var aggregateId = Guid.NewGuid();

        var events = new IDomainEvent[]
        {
            new CartCreated(aggregateId, DateTimeOffset.UtcNow, "alice"),
            new ItemAddedToCart(aggregateId, DateTimeOffset.UtcNow, "SKU-1", 2),
        };

        await store.AppendEventsAsync(aggregateId, "Cart", events, expectedVersion: -1);
        var loaded = await store.LoadEventStreamAsync(aggregateId, "Cart");
        loaded.Events.Should().HaveCount(2);
        loaded.Events.OfType<CartCreated>().First().Owner.Should().Be("alice");
        loaded.Events.OfType<ItemAddedToCart>().First().Sku.Should().Be("SKU-1");
    }

    [Fact]
    public async Task EfCoreEventStore_throws_on_concurrency_conflict()
    {
        var factory = InMemoryFactory(nameof(EfCoreEventStore_throws_on_concurrency_conflict));
        var store = new EfCoreEventStore(factory, new[] { typeof(CartCreated) });
        var id = Guid.NewGuid();
        await store.AppendEventsAsync(id, "Cart", new[] { new CartCreated(id, DateTimeOffset.UtcNow, "alice") }, expectedVersion: -1);
        await FluentActions.Invoking(() => store.AppendEventsAsync(id, "Cart",
                new[] { new CartCreated(id, DateTimeOffset.UtcNow, "bob") }, expectedVersion: 5))
            .Should().ThrowAsync<ConcurrencyException>();
    }

    [Fact]
    public async Task EfCoreSnapshotStore_round_trip()
    {
        var factory = InMemoryFactory(nameof(EfCoreSnapshotStore_round_trip));
        var snapshots = new EfCoreSnapshotStore(factory);
        var id = Guid.NewGuid();
        await snapshots.SaveSnapshotAsync(new AggregateSnapshot(id, "Cart", 5, "{\"items\":3}", DateTimeOffset.UtcNow));
        var latest = await snapshots.LoadLatestSnapshotAsync(id, "Cart");
        latest.Should().NotBeNull();
        latest!.AggregateVersion.Should().Be(5);
        (await snapshots.GetLatestSnapshotVersionAsync(id, "Cart")).Should().Be(5);
    }

    private sealed class InMemoryFactoryImpl(DbContextOptions<EventStoreDbContext> options) : IDbContextFactory<EventStoreDbContext>
    {
        public EventStoreDbContext CreateDbContext() => new(options);
    }
}
