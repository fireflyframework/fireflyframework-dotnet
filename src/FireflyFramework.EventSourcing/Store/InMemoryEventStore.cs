using System.Runtime.CompilerServices;
using System.Text.Json;
using FireflyFramework.EventSourcing.Domain;

namespace FireflyFramework.EventSourcing.Store;

/// <summary>
/// In-memory implementation suitable for tests and small workloads. Persistent
/// implementations should be backed by EF Core / Dapper against PostgreSQL.
/// </summary>
public sealed class InMemoryEventStore : IEventStore
{
    private readonly object _lock = new();
    private readonly Dictionary<(Guid id, string type), List<(StoredEventEnvelope Envelope, IDomainEvent Event)>> _byAggregate = new();
    private readonly List<StoredEventEnvelope> _byGlobalOrder = new();
    private long _globalSeq;

    public Task<EventStream> AppendEventsAsync(
        Guid aggregateId,
        string aggregateType,
        IEnumerable<IDomainEvent> events,
        long expectedVersion,
        Dictionary<string, string>? metadata = null,
        CancellationToken ct = default)
    {
        var list = events.ToList();
        lock (_lock)
        {
            var key = (aggregateId, aggregateType);
            var current = _byAggregate.TryGetValue(key, out var existing) ? existing.Count - 1 : -1;
            if (expectedVersion != current)
            {
                throw new ConcurrencyException(
                    $"Aggregate {aggregateType}:{aggregateId} expected version {expectedVersion}, actual {current}");
            }

            existing ??= new List<(StoredEventEnvelope, IDomainEvent)>();
            foreach (var @event in list)
            {
                var envelope = new StoredEventEnvelope(
                    ++_globalSeq,
                    aggregateId,
                    ++current,
                    aggregateType,
                    @event.EventType,
                    @event.EventVersion,
                    JsonSerializer.Serialize(@event, @event.GetType()),
                    metadata,
                    @event.Timestamp,
                    null);

                existing.Add((envelope, @event));
                _byGlobalOrder.Add(envelope);
            }

            _byAggregate[key] = existing;
            return Task.FromResult(new EventStream(aggregateId, aggregateType, list, current));
        }
    }

    public Task<EventStream> LoadEventStreamAsync(
        Guid aggregateId, string aggregateType, long fromVersion = 0, long? toVersion = null, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (!_byAggregate.TryGetValue((aggregateId, aggregateType), out var list))
            {
                return Task.FromResult(new EventStream(aggregateId, aggregateType, Array.Empty<IDomainEvent>(), -1));
            }

            var sliced = list
                .Where(t => t.Envelope.AggregateVersion >= fromVersion && (toVersion is null || t.Envelope.AggregateVersion <= toVersion))
                .Select(t => t.Event)
                .ToList();
            return Task.FromResult(new EventStream(aggregateId, aggregateType, sliced, list.Count - 1));
        }
    }

    public Task<long> GetAggregateVersionAsync(Guid aggregateId, string aggregateType, CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_byAggregate.TryGetValue((aggregateId, aggregateType), out var list) ? (long)(list.Count - 1) : -1);
        }
    }

    public Task<bool> AggregateExistsAsync(Guid aggregateId, string aggregateType, CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_byAggregate.ContainsKey((aggregateId, aggregateType)));
        }
    }

    public async IAsyncEnumerable<StoredEventEnvelope> StreamAllEventsAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        foreach (var e in _byGlobalOrder.ToArray())
        {
            ct.ThrowIfCancellationRequested();
            yield return e;
        }
    }

    public async IAsyncEnumerable<StoredEventEnvelope> StreamAllEventsFromAsync(long globalSequence, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        foreach (var e in _byGlobalOrder.Where(e => e.GlobalSequence > globalSequence).ToArray())
        {
            ct.ThrowIfCancellationRequested();
            yield return e;
        }
    }
}
