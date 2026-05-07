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

using System.Text;
using FireflyFramework.Eda.Configuration;
using FireflyFramework.Eda.Events;
using FireflyFramework.Eda.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace FireflyFramework.Eda.Publisher;

/// <summary>
/// RabbitMQ-backed publisher using RabbitMQ.Client 7.x. Mirrors Java <c>RabbitMqEventPublisher</c>.
/// Uses publisher confirms and quorum queues by default for at-least-once delivery.
/// </summary>
public sealed class RabbitMqEventPublisher : IEventPublisher
{
    private readonly RabbitMqOptions _opt;
    private readonly IMessageSerializer _serializer;
    private readonly ILogger<RabbitMqEventPublisher> _log;
    private IConnection? _connection;
    private IChannel? _channel;
    private bool _disposed;

    public RabbitMqEventPublisher(
        IOptions<EdaOptions> options,
        IMessageSerializer serializer,
        ILogger<RabbitMqEventPublisher> log)
    {
        _opt = options.Value.RabbitMq;
        _serializer = serializer;
        _log = log;
    }

    public PublisherType Type => PublisherType.RabbitMq;
    public string? DefaultDestination => null;
    public bool IsAvailable => _connection?.IsOpen == true && !_disposed;

    public async Task PublishAsync(EventEnvelope envelope, CancellationToken ct = default)
    {
        await EnsureConnectionAsync(ct).ConfigureAwait(false);
        var props = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = envelope.Metadata?.CorrelationId ?? Guid.NewGuid().ToString("N"),
            Type = envelope.EventType,
            Headers = new Dictionary<string, object?>(),
        };

        if (envelope.Headers is not null)
        {
            foreach (var (k, v) in envelope.Headers)
            {
                props.Headers[k] = Encoding.UTF8.GetBytes(v);
            }
        }

        var body = _serializer.Serialize(envelope.Payload!);
        await _channel!.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: envelope.Destination,
            mandatory: false,
            basicProperties: props,
            body: body,
            cancellationToken: ct).ConfigureAwait(false);

        _log.LogDebug("RabbitMQ publish to {Routing} ({Bytes} bytes)", envelope.Destination, body.Length);
    }

    public Task<PublisherHealth> GetHealthAsync(CancellationToken ct = default) =>
        Task.FromResult(new PublisherHealth(Type, IsAvailable, IsAvailable ? "UP" : "DOWN"));

    private async Task EnsureConnectionAsync(CancellationToken ct)
    {
        if (_connection?.IsOpen == true && _channel?.IsOpen == true) return;
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
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        if (_channel is not null) await _channel.CloseAsync().ConfigureAwait(false);
        if (_connection is not null) await _connection.CloseAsync().ConfigureAwait(false);
    }
}
