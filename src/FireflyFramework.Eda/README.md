# FireflyFramework.Eda

Unified event-driven architecture: publishers and consumers over Kafka, RabbitMQ or in-memory channels with JSON / Protobuf / Avro serializers, ack callbacks, headers, metadata and Polly resilience hooks. Mirrors `fireflyframework-eda`.

## Quick start

```csharp
builder.Services.AddFireflyEda(builder.Configuration);

// Publish
var publisher = sp.GetRequiredService<IEventPublisher>();
await publisher.PublishAsync(EventEnvelope.ForPublishing("orders.created", "OrderCreated", new { orderId = 42 }));

// Consume
var consumer = sp.GetRequiredService<IEventConsumer>();
await consumer.StartAsync();
await foreach (var envelope in consumer.ConsumeAsync(new[] { "orders.created" }, ct))
{
    // process
    await envelope.Ack?.AcknowledgeAsync(ct);
}
```

## Configuration

```jsonc
{
  "Firefly": {
    "Eda": {
      "DefaultPublisher": "Kafka",          // Kafka | RabbitMq | InMemory | Auto
      "DefaultConsumer": "Kafka",
      "Kafka": {
        "BootstrapServers": "localhost:9092",
        "GroupId": "orders-service"
      },
      "RabbitMq": {
        "Hostname": "localhost",
        "Port": 5672,
        "Username": "guest",
        "Password": "guest",
        "VirtualHost": "/"
      }
    }
  }
}
```

## Implementations included

| Adapter | Class | Notes |
|---|---|---|
| Kafka publisher | `KafkaEventPublisher` | Confluent.Kafka 2.6, idempotent producer, all-acks |
| Kafka consumer | (planned for the same backend; current default is in-memory) | |
| RabbitMQ publisher | `RabbitMqEventPublisher` | RabbitMQ.Client 7.x, persistent delivery |
| RabbitMQ consumer | `RabbitMqEventConsumer` | AsyncEventingBasicConsumer with manual acks |
| In-memory bus | `InMemoryEventPublisher` + `InMemoryEventConsumer` | Per-process Channel<T>; useful for tests |
| Noop | `NoopEventPublisher` (planned) | |

## Serializers

| Format | Class | Backing |
|---|---|---|
| JSON | `JsonMessageSerializer` | `System.Text.Json` (default) |
| Protobuf | `ProtobufMessageSerializer` | `Google.Protobuf` |
| Avro | `AvroMessageSerializer` | Apache Avro (`Avro.Specific`) |

Implement `IMessageSerializer` to add Schema-Registry-aware variants.

## Annotations

- `[EventPublisher]` — marks a method whose return value is published.
- `[PublishResult]` — publishes the method result after successful execution.
- `[EventListener]` — registers a method as an event listener (consumer-side).
- `ErrorHandlingStrategy.{LogAndContinue,Retry,DeadLetterQueue,Throw}` — control consumer error semantics.
