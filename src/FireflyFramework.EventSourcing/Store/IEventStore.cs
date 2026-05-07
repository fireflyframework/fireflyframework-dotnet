using FireflyFramework.EventSourcing.Domain;

namespace FireflyFramework.EventSourcing.Store;

public sealed record StoredEventEnvelope(
    long GlobalSequence,
    Guid AggregateId,
    long AggregateVersion,
    string AggregateType,
    string EventType,
    int EventVersion,
    string Payload,
    Dictionary<string, string>? Headers,
    DateTimeOffset Timestamp,
    string? TenantId);

public sealed record EventStream(Guid AggregateId, string AggregateType, IReadOnlyList<IDomainEvent> Events, long Version);

public sealed class ConcurrencyException : Kernel.Exceptions.FireflyException
{
    public ConcurrencyException(string message) : base(message, "ES_CONCURRENCY_VIOLATION") { }
}

/// <summary>Append-only event store contract. Mirrors Java <c>EventStore</c>.</summary>
public interface IEventStore
{
    Task<EventStream> AppendEventsAsync(
        Guid aggregateId,
        string aggregateType,
        IEnumerable<IDomainEvent> events,
        long expectedVersion,
        Dictionary<string, string>? metadata = null,
        CancellationToken ct = default);

    Task<EventStream> LoadEventStreamAsync(
        Guid aggregateId,
        string aggregateType,
        long fromVersion = 0,
        long? toVersion = null,
        CancellationToken ct = default);

    Task<long> GetAggregateVersionAsync(Guid aggregateId, string aggregateType, CancellationToken ct = default);

    Task<bool> AggregateExistsAsync(Guid aggregateId, string aggregateType, CancellationToken ct = default);

    IAsyncEnumerable<StoredEventEnvelope> StreamAllEventsAsync(CancellationToken ct = default);

    IAsyncEnumerable<StoredEventEnvelope> StreamAllEventsFromAsync(long globalSequence, CancellationToken ct = default);
}
