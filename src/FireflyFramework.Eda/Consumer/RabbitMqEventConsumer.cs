using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using FireflyFramework.Eda.Configuration;
using FireflyFramework.Eda.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FireflyFramework.Eda.Consumer;

/// <summary>
/// RabbitMQ consumer using AsyncBasicConsumer. Mirrors Java <c>RabbitMqEventConsumer</c>.
/// </summary>
public sealed class RabbitMqEventConsumer : IEventConsumer
{
    private readonly RabbitMqOptions _opt;
    private readonly ILogger<RabbitMqEventConsumer> _log;
    private IConnection? _connection;
    private IChannel? _channel;
    private bool _running;

    public RabbitMqEventConsumer(IOptions<EdaOptions> options, ILogger<RabbitMqEventConsumer> log)
    {
        _opt = options.Value.RabbitMq;
        _log = log;
    }

    public ConsumerType Type => ConsumerType.RabbitMq;
    public bool IsRunning => _running;
    public bool IsAvailable => _connection?.IsOpen == true;

    public async Task StartAsync(CancellationToken ct = default)
    {
        var factory = new ConnectionFactory
        {
            HostName = _opt.Hostname,
            Port = _opt.Port,
            UserName = _opt.Username,
            Password = _opt.Password,
            VirtualHost = _opt.VirtualHost,
        };

        _connection = await factory.CreateConnectionAsync(ct).ConfigureAwait(false);
        _channel = await _connection.CreateChannelAsync(cancellationToken: ct).ConfigureAwait(false);
        _running = true;
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        _running = false;
        if (_channel is not null) await _channel.CloseAsync().ConfigureAwait(false);
        if (_connection is not null) await _connection.CloseAsync().ConfigureAwait(false);
    }

    public async IAsyncEnumerable<EventEnvelope> ConsumeAsync(
        IEnumerable<string> destinations,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!_running) await StartAsync(ct).ConfigureAwait(false);
        if (_channel is null) yield break;

        var queue = Channel.CreateUnbounded<EventEnvelope>();
        foreach (var dest in destinations)
        {
            await _channel.QueueDeclareAsync(dest, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct).ConfigureAwait(false);
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (_, args) =>
            {
                var headers = args.BasicProperties.Headers?.ToDictionary(
                    p => p.Key,
                    p => p.Value is byte[] b ? Encoding.UTF8.GetString(b) : p.Value?.ToString() ?? string.Empty)
                    ?? new();

                var envelope = new EventEnvelope(
                    args.RoutingKey,
                    args.BasicProperties.Type ?? "unknown",
                    args.Body.ToArray(),
                    Headers: headers,
                    Timestamp: DateTimeOffset.UtcNow,
                    PublisherType: PublisherType.RabbitMq,
                    ConsumerType: ConsumerType.RabbitMq,
                    Ack: new RabbitAckCallback(_channel, args.DeliveryTag));

                await queue.Writer.WriteAsync(envelope, ct).ConfigureAwait(false);
            };

            await _channel.BasicConsumeAsync(dest, autoAck: false, consumer: consumer, cancellationToken: ct).ConfigureAwait(false);
            _log.LogInformation("Subscribed to RabbitMQ queue {Queue}", dest);
        }

        await foreach (var envelope in queue.Reader.ReadAllAsync(ct))
        {
            yield return envelope;
        }
    }

    public Task<ConsumerHealth> GetHealthAsync(CancellationToken ct = default) =>
        Task.FromResult(new ConsumerHealth(Type, IsAvailable, _running, _running ? "UP" : "DOWN"));

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    private sealed class RabbitAckCallback(IChannel channel, ulong deliveryTag) : IAckCallback
    {
        public Task AcknowledgeAsync(CancellationToken ct = default) =>
            channel.BasicAckAsync(deliveryTag, multiple: false, cancellationToken: ct).AsTask();

        public Task RejectAsync(Exception error, CancellationToken ct = default) =>
            channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, cancellationToken: ct).AsTask();
    }
}
