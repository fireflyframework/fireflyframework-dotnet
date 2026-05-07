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
