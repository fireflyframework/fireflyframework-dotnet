# FireflyFramework.Eda

Unified event-driven architecture: publishers and consumers over Kafka,
RabbitMQ, or in-memory channels with JSON / Protobuf / Avro serialisers
(Schema Registry variants for Kafka), per-message ack callbacks, header
propagation, filter chain, error-handler chain, and a Polly-backed
resilient publisher wrapper. Mirrors `org.fireflyframework:firefly-common-eda`.

## Wiring

```csharp
using FireflyFramework.Eda.DependencyInjection;
using FireflyFramework.Eda.Events;
using FireflyFramework.Eda.Publisher;
using FireflyFramework.Eda.Consumer;

builder.Services.AddFireflyEda(builder.Configuration);
```

## Publishing

```csharp
var publisher = sp.GetRequiredService<IEventPublisher>();
var envelope  = EventEnvelope
    .ForPublishing("orders.created", "OrderCreated", new { orderId = 42 })
    .WithHeaders(new Dictionary<string, string> { ["tenant-id"] = "alpha" });
await publisher.PublishAsync(envelope, ct);
```

Wrap any publisher in retry + circuit-breaker + timeout:

```csharp
var resilient = new ResilientEventPublisher(
    inner: kafkaPublisher,
    log:   logger,
    options: new ResilientPublisherOptions
    {
        RetryAttempts = 5,
        RetryDelay    = TimeSpan.FromMilliseconds(100),
        BreakDuration = TimeSpan.FromSeconds(30),
    });
```

## Consuming

```csharp
var consumer = sp.GetRequiredService<IEventConsumer>();
await consumer.StartAsync(ct);

await foreach (var envelope in consumer.ConsumeAsync(new[] { "orders.created" }, ct))
{
    try
    {
        // ... process the event
        if (envelope.Ack is { } ack)
        {
            await ack.AcknowledgeAsync(ct);
        }
    }
    catch (Exception ex)
    {
        if (envelope.Ack is { } ack)
        {
            await ack.RejectAsync(ex, ct);   // Kafka: seek-back; RabbitMQ: nack+requeue
        }
    }
}
```

## Public surface

### Publishers

| Type                       | Backing                                                    |
|----------------------------|------------------------------------------------------------|
| `IEventPublisher`          | Port: `Type`, `IsAvailable`, `PublishAsync`, `GetHealthAsync` |
| `KafkaEventPublisher`      | Confluent.Kafka 2.6, idempotent producer, transactional support |
| `RabbitMqEventPublisher`   | RabbitMQ.Client 7.x with publisher confirms                |
| `InMemoryEventPublisher`   | `Channel<T>` per destination via `InMemoryEventBus`        |
| `ResilientEventPublisher`  | Polly v8 pipeline: retry → circuit-breaker → timeout       |

### Consumers

| Type                       | Backing                                                    |
|----------------------------|------------------------------------------------------------|
| `IEventConsumer`           | Port: `Type`, `IsRunning`, `IsAvailable`, `Start/Stop`, `ConsumeAsync` |
| `KafkaEventConsumer`       | Manual offset commit (`KafkaAckCallback` commits on Acknowledge, seeks-back on Reject) for at-least-once semantics |
| `RabbitMqEventConsumer`    | `AsyncEventingBasicConsumer` with manual ack/nack          |
| `InMemoryEventConsumer`    | Reads from `InMemoryEventBus` channels                     |

### Serialisers

| Format    | Class                                  | Backing                          |
|-----------|----------------------------------------|----------------------------------|
| JSON      | `JsonMessageSerializer`                | `System.Text.Json`               |
| Protobuf  | `ProtobufMessageSerializer`            | `Google.Protobuf`                |
| Avro      | `AvroMessageSerializer`                | `Apache.Avro`                    |
| Schema Registry Avro     | `SchemaRegistryAvroSerializer<T>`     | `Confluent.SchemaRegistry.Serdes.Avro` |
| Schema Registry Protobuf | `SchemaRegistryProtobufSerializer<T>` | `Confluent.SchemaRegistry.Serdes.Protobuf` |

Implement `IMessageSerializer` to add custom formats.

### Filters

Pre-delivery predicates that compose into pipelines.

| Filter                     | Accepts when                                                         |
|----------------------------|----------------------------------------------------------------------|
| `EventTypeFilter`          | Event type matches a literal or `prefix.*` wildcard                  |
| `DestinationEventFilter`   | Envelope destination is in the allowed set                           |
| `HeaderEventFilter`        | A specific header is present (and optionally has a specific value)   |
| `CompositeEventFilter`     | All child filters accept                                             |

### Error handling

| Type                           | Purpose                                                                |
|--------------------------------|------------------------------------------------------------------------|
| `ErrorHandlingStrategy`        | `Ignore`, `Retry`, `Halt`, `DeadLetter`                                |
| `IErrorHandler`                | `HandleAsync(envelope, error, attempt)` returns a strategy             |
| `DefaultErrorHandler`          | Retries up to `MaxRetries`, then dead-letters                          |
| `MetricsErrorHandler`          | Records error counts before delegating                                 |
| `ChainErrorHandler`            | Tries each handler in order; first non-Ignore decision wins            |

### `EventEnvelope`

Per-message metadata record carried through both publisher and consumer
sides:

```csharp
public sealed record EventEnvelope(
    string Destination, string EventType, object? Payload,
    string? TransactionId = null,
    Dictionary<string, string>? Headers = null,
    EventMetadata? Metadata = null,
    DateTimeOffset Timestamp = default,
    PublisherType PublisherType = PublisherType.Auto,
    ConsumerType? ConsumerType = null,
    string? ConnectionId = null,
    IAckCallback? Ack = null);
```

`EventMetadata` carries optional CorrelationId, CausationId, Version,
Source, UserId, SessionId, TenantId, plus an open Custom dictionary.

## Configuration

```json
{
  "Firefly": {
    "Eda": {
      "DefaultPublisher": "Kafka",
      "DefaultConsumer":  "Kafka",
      "Kafka": {
        "BootstrapServers":  "localhost:9092",
        "GroupId":           "orders-service",
        "SchemaRegistryUrl": "http://localhost:8081"
      },
      "RabbitMq": {
        "Hostname":    "localhost",
        "Port":        5672,
        "Username":    "guest",
        "Password":    "guest",
        "VirtualHost": "/"
      }
    }
  }
}
```

## Dependencies

| Reference                                 | Used for                          |
|-------------------------------------------|-----------------------------------|
| `FireflyFramework.Kernel`                 | Base exceptions                   |
| `Confluent.Kafka`                         | Kafka publisher / consumer        |
| `Confluent.SchemaRegistry.Serdes.{Avro,Protobuf}` | Schema Registry serialisers |
| `RabbitMQ.Client`                         | RabbitMQ publisher / consumer     |
| `Polly.Core`                              | Resilient publisher pipeline      |

## Java mapping

| .NET                          | Java                              |
|-------------------------------|-----------------------------------|
| `IEventPublisher`             | `EventPublisher`                  |
| `IEventConsumer`              | `EventConsumer`                   |
| `EventEnvelope`               | `EventEnvelope`                   |
| `KafkaEventPublisher`         | `KafkaEventPublisher`             |
| `RabbitMqEventPublisher`      | `RabbitMqEventPublisher`          |
| `ResilientEventPublisher`     | `ResilientEventPublisher`         |
| `IEventFilter` family         | `EventFilter` + 4 implementations |
| `IErrorHandler` chain         | `CustomErrorHandler` + registry   |
