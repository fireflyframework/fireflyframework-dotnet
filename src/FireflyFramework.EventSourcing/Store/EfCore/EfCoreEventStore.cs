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

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FireflyFramework.EventSourcing.Domain;
using Microsoft.EntityFrameworkCore;

namespace FireflyFramework.EventSourcing.Store.EfCore;

/// <summary>
/// Production-ready <see cref="IEventStore"/> backed by EF Core. Mirrors Java
/// <c>R2dbcEventStore</c>: append-only writes, optimistic concurrency check via the
/// unique (aggregateType, aggregateId, version) index, and an outbox row per event for
/// transactional publishing.
/// </summary>
public sealed class EfCoreEventStore : IEventStore
{
    private readonly IDbContextFactory<EventStoreDbContext> _factory;
    private readonly Type[] _knownEventTypes;

    public EfCoreEventStore(IDbContextFactory<EventStoreDbContext> factory, IEnumerable<Type>? knownEventTypes = null)
    {
        _factory = factory;
        _knownEventTypes = knownEventTypes?.ToArray() ?? Array.Empty<Type>();
    }

    public async Task<EventStream> AppendEventsAsync(
        Guid aggregateId,
        string aggregateType,
        IEnumerable<IDomainEvent> events,
        long expectedVersion,
        Dictionary<string, string>? metadata = null,
        CancellationToken ct = default)
    {
        var list = events.ToList();
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        var current = await db.Events
            .Where(e => e.AggregateType == aggregateType && e.AggregateId == aggregateId)
            .OrderByDescending(e => e.AggregateVersion)
            .Select(e => (long?)e.AggregateVersion)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false) ?? -1;

        if (current != expectedVersion)
        {
            throw new ConcurrencyException(
                $"Aggregate {aggregateType}:{aggregateId} expected version {expectedVersion}, actual {current}");
        }

        var nextVersion = current;
        var entities = new List<EventEntity>();
        foreach (var @event in list)
        {
            var entity = new EventEntity
            {
                AggregateId = aggregateId,
                AggregateVersion = ++nextVersion,
                AggregateType = aggregateType,
                EventType = @event.EventType,
                EventVersion = @event.EventVersion,
                Payload = JsonSerializer.Serialize(@event, @event.GetType()),
                HeadersJson = metadata is null ? null : JsonSerializer.Serialize(metadata),
                Timestamp = @event.Timestamp,
            };

            entities.Add(entity);
            db.Events.Add(entity);
            db.Outbox.Add(new EventOutboxEntity
            {
                EventType = entity.EventType,
                Destination = aggregateType,
                Payload = entity.Payload,
            });
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return new EventStream(aggregateId, aggregateType, list, nextVersion);
    }

    public async Task<EventStream> LoadEventStreamAsync(
        Guid aggregateId, string aggregateType, long fromVersion = 0, long? toVersion = null, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var query = db.Events
            .Where(e => e.AggregateType == aggregateType && e.AggregateId == aggregateId && e.AggregateVersion >= fromVersion);
        if (toVersion is not null)
        {
            query = query.Where(e => e.AggregateVersion <= toVersion);
        }

        var rows = await query.OrderBy(e => e.AggregateVersion).ToListAsync(ct).ConfigureAwait(false);
        var events = rows.Select(Materialize).ToList();
        var version = rows.Count == 0 ? -1 : rows[^1].AggregateVersion;
        return new EventStream(aggregateId, aggregateType, events, version);
    }

    public async Task<long> GetAggregateVersionAsync(Guid aggregateId, string aggregateType, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Events
            .Where(e => e.AggregateType == aggregateType && e.AggregateId == aggregateId)
            .OrderByDescending(e => e.AggregateVersion)
            .Select(e => (long?)e.AggregateVersion)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false) ?? -1;
    }

    public async Task<bool> AggregateExistsAsync(Guid aggregateId, string aggregateType, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Events.AnyAsync(e => e.AggregateType == aggregateType && e.AggregateId == aggregateId, ct).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<StoredEventEnvelope> StreamAllEventsAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await foreach (var row in db.Events.AsNoTracking().OrderBy(e => e.GlobalSequence).AsAsyncEnumerable().WithCancellation(ct))
        {
            yield return ToEnvelope(row);
        }
    }

    public async IAsyncEnumerable<StoredEventEnvelope> StreamAllEventsFromAsync(long globalSequence, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await foreach (var row in db.Events.AsNoTracking()
                           .Where(e => e.GlobalSequence > globalSequence)
                           .OrderBy(e => e.GlobalSequence)
                           .AsAsyncEnumerable().WithCancellation(ct))
        {
            yield return ToEnvelope(row);
        }
    }

    private IDomainEvent Materialize(EventEntity entity)
    {
        var clrType = _knownEventTypes
            .FirstOrDefault(t => t.GetCustomAttribute<Annotations.DomainEventAttribute>()?.EventType == entity.EventType
                                  || t.Name == entity.EventType);

        if (clrType is null)
        {
            throw new InvalidOperationException($"Unknown event type '{entity.EventType}' — register it via the EfCoreEventStore constructor.");
        }

        return (IDomainEvent)JsonSerializer.Deserialize(entity.Payload, clrType)!;
    }

    private static StoredEventEnvelope ToEnvelope(EventEntity e) => new(
        e.GlobalSequence, e.AggregateId, e.AggregateVersion, e.AggregateType, e.EventType,
        e.EventVersion, e.Payload,
        e.HeadersJson is null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(e.HeadersJson),
        e.Timestamp, e.TenantId);
}
