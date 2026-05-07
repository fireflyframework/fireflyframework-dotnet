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

namespace FireflyFramework.Eda.Events;

public enum PublisherType { Kafka, RabbitMq, InMemory, Auto, Noop }
public enum ConsumerType { Kafka, RabbitMq, InMemory, Auto, Noop }
public enum SerializationFormat { Json, Protobuf, Avro }

/// <summary>Per-message metadata. Mirrors Java <c>EventMetadata</c>.</summary>
public sealed record EventMetadata(
    string? CorrelationId = null,
    string? CausationId = null,
    string? Version = null,
    string? Source = null,
    string? UserId = null,
    string? SessionId = null,
    string? TenantId = null,
    Dictionary<string, object?>? Custom = null);

/// <summary>Acknowledgement callback for consumer-side delivery confirmation.</summary>
public interface IAckCallback
{
    Task AcknowledgeAsync(CancellationToken ct = default);
    Task RejectAsync(Exception error, CancellationToken ct = default);
}

/// <summary>Event envelope. Mirrors Java <c>EventEnvelope</c>.</summary>
public sealed record EventEnvelope(
    string Destination,
    string EventType,
    object? Payload,
    string? TransactionId = null,
    Dictionary<string, string>? Headers = null,
    EventMetadata? Metadata = null,
    DateTimeOffset Timestamp = default,
    PublisherType PublisherType = PublisherType.Auto,
    ConsumerType? ConsumerType = null,
    string? ConnectionId = null,
    IAckCallback? Ack = null)
{
    public static EventEnvelope ForPublishing(string destination, string eventType, object payload) =>
        new(destination, eventType, payload, Timestamp: DateTimeOffset.UtcNow);

    public EventEnvelope WithHeaders(Dictionary<string, string> h) => this with { Headers = h };
    public EventEnvelope WithMetadata(EventMetadata m) => this with { Metadata = m };
}
