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

using Confluent.Kafka;
using FireflyFramework.Eda.Configuration;
using FireflyFramework.Eda.Events;
using FireflyFramework.Eda.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FireflyFramework.Eda.Publisher;

/// <summary>Kafka-backed publisher using Confluent.Kafka. Mirrors Java <c>KafkaEventPublisher</c>.</summary>
public sealed class KafkaEventPublisher : IEventPublisher
{
    private readonly IProducer<string, byte[]> _producer;
    private readonly IMessageSerializer _serializer;
    private readonly ILogger<KafkaEventPublisher> _log;
    private bool _disposed;

    public KafkaEventPublisher(
        IOptions<EdaOptions> options,
        IMessageSerializer serializer,
        ILogger<KafkaEventPublisher> log)
    {
        var cfg = new ProducerConfig
        {
            BootstrapServers = options.Value.Kafka.BootstrapServers,
            EnableIdempotence = true,
            Acks = Acks.All,
        };

        _producer = new ProducerBuilder<string, byte[]>(cfg).Build();
        _serializer = serializer;
        _log = log;
    }

    public PublisherType Type => PublisherType.Kafka;
    public string? DefaultDestination => null;
    public bool IsAvailable => !_disposed;

    public async Task PublishAsync(EventEnvelope envelope, CancellationToken ct = default)
    {
        var payload = _serializer.Serialize(envelope.Payload!);
        var msg = new Message<string, byte[]>
        {
            Key = envelope.Metadata?.CorrelationId ?? envelope.EventType,
            Value = payload,
            Headers = BuildHeaders(envelope),
        };

        var result = await _producer.ProduceAsync(envelope.Destination, msg, ct).ConfigureAwait(false);
        _log.LogDebug("Kafka publish to {Topic} partition {Partition} offset {Offset}",
            result.Topic, result.Partition.Value, result.Offset.Value);
    }

    private static Headers BuildHeaders(EventEnvelope envelope)
    {
        var headers = new Headers
        {
            { "eventType", System.Text.Encoding.UTF8.GetBytes(envelope.EventType) },
        };

        if (envelope.Headers is not null)
        {
            foreach (var (k, v) in envelope.Headers)
            {
                headers.Add(k, System.Text.Encoding.UTF8.GetBytes(v));
            }
        }

        return headers;
    }

    public Task<PublisherHealth> GetHealthAsync(CancellationToken ct = default) =>
        Task.FromResult(new PublisherHealth(Type, !_disposed, _disposed ? "DOWN" : "UP"));

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
        return ValueTask.CompletedTask;
    }
}
