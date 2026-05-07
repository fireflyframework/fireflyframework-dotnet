using System.Runtime.CompilerServices;
using System.Text;
using Confluent.Kafka;
using FireflyFramework.Eda.Configuration;
using FireflyFramework.Eda.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FireflyFramework.Eda.Consumer;

/// <summary>
/// Kafka consumer with manual offset commit. Mirrors Java <c>KafkaEventConsumer</c>.
/// </summary>
/// <remarks>
/// Each yielded <see cref="EventEnvelope"/> carries an <see cref="IAckCallback"/> that
/// commits or seeks-back the offset on acknowledge / reject. AutoCommit is disabled to
/// give the consumer code at-least-once semantics.
/// </remarks>
public sealed class KafkaEventConsumer : IEventConsumer
{
    private readonly EdaOptions _opt;
    private readonly ILogger<KafkaEventConsumer> _log;
    private IConsumer<string, byte[]>? _consumer;
    private bool _running;

    public KafkaEventConsumer(IOptions<EdaOptions> options, ILogger<KafkaEventConsumer> log)
    {
        _opt = options.Value;
        _log = log;
    }

    public ConsumerType Type => ConsumerType.Kafka;
    public bool IsRunning => _running;
    public bool IsAvailable => _consumer is not null;

    public Task StartAsync(CancellationToken ct = default)
    {
        if (_consumer is not null) return Task.CompletedTask;
        var cfg = new ConsumerConfig
        {
            BootstrapServers = _opt.Kafka.BootstrapServers,
            GroupId = _opt.Kafka.GroupId ?? "firefly-consumer",
            EnableAutoCommit = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnablePartitionEof = false,
        };

        _consumer = new ConsumerBuilder<string, byte[]>(cfg).Build();
        _running = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        _running = false;
        _consumer?.Close();
        _consumer?.Dispose();
        _consumer = null;
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<EventEnvelope> ConsumeAsync(
        IEnumerable<string> destinations,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (_consumer is null) await StartAsync(ct).ConfigureAwait(false);
        var consumer = _consumer ?? throw new InvalidOperationException("Consumer not started");
        consumer.Subscribe(destinations);
        _log.LogInformation("Kafka consumer subscribed to {Topics}", string.Join(", ", destinations));

        while (!ct.IsCancellationRequested && _running)
        {
            ConsumeResult<string, byte[]>? result = null;
            try
            {
                // ConsumeAsync isn't part of Confluent.Kafka; consume returns synchronously.
                // Yield to the scheduler between polls to keep the async iterator cooperative.
                result = await Task.Run(() => consumer.Consume(TimeSpan.FromMilliseconds(500)), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (ConsumeException ex)
            {
                _log.LogWarning(ex, "Kafka consume error: {Reason}", ex.Error.Reason);
                continue;
            }

            if (result is null || result.IsPartitionEOF)
            {
                continue;
            }

            var headers = new Dictionary<string, string>();
            if (result.Message.Headers is not null)
            {
                foreach (var h in result.Message.Headers)
                {
                    headers[h.Key] = Encoding.UTF8.GetString(h.GetValueBytes());
                }
            }

            var eventType = headers.TryGetValue("eventType", out var et) ? et : "unknown";
            var ack = new KafkaAckCallback(consumer, result.TopicPartitionOffset);
            yield return new EventEnvelope(
                result.Topic,
                eventType,
                result.Message.Value,
                Headers: headers,
                Timestamp: result.Message.Timestamp.UtcDateTime,
                PublisherType: PublisherType.Kafka,
                ConsumerType: ConsumerType.Kafka,
                Ack: ack);
        }
    }

    public Task<ConsumerHealth> GetHealthAsync(CancellationToken ct = default) =>
        Task.FromResult(new ConsumerHealth(Type, IsAvailable, _running, _running ? "UP" : "DOWN"));

    public ValueTask DisposeAsync()
    {
        StopAsync().GetAwaiter().GetResult();
        return ValueTask.CompletedTask;
    }

    private sealed class KafkaAckCallback(IConsumer<string, byte[]> consumer, TopicPartitionOffset offset) : IAckCallback
    {
        public Task AcknowledgeAsync(CancellationToken ct = default)
        {
            consumer.Commit(new[] { offset });
            return Task.CompletedTask;
        }

        public Task RejectAsync(Exception error, CancellationToken ct = default)
        {
            // Seek back so the offset is replayed on next poll
            consumer.Seek(offset);
            return Task.CompletedTask;
        }
    }
}
