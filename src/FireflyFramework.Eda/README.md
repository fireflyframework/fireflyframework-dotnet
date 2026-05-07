# FireflyFramework.Eda

Unified event-driven architecture for the framework. One set of
abstractions — `IEventPublisher`, `IEventConsumer`, `EventEnvelope`,
`IMessageSerializer`, `IEventFilter`, `IErrorHandler` — backed by
**Apache Kafka**, **RabbitMQ**, or an **in-memory channel** for tests
and single-host deployments. JSON / Protobuf / Avro serialisation
out of the box; Confluent Schema Registry variants for Kafka.

Mirrors `org.fireflyframework:firefly-common-eda`. The wire shape
(envelope fields, header names, error semantics) is identical to the
Java side, so a Kafka topic produced by a Java service is consumed
by a .NET service and vice versa.

---

## Why a unified EDA module?

Three problems show up in every event-driven service that adopts a
specific broker SDK directly:

1. **Vendor lock-in.** A handler that takes a `IConsumer<TKey, TValue>`
   from Confluent.Kafka can never run against RabbitMQ without rewrite.
2. **Inconsistent ack semantics.** Kafka's "commit offset", RabbitMQ's
   "basic.ack", and an in-memory channel's "drain it" are three
   wholly different mental models.
3. **Missing cross-cutting concerns.** Headers (correlation, tenant),
   filters (skip uninteresting events), error handling (retry vs.
   dead-letter), and resilience (circuit-breaker, retry-with-jitter)
   end up bolted on per-team.

`FireflyFramework.Eda` solves all three with a single abstraction
layer: handlers code against the framework's port, and the broker
underneath is configurable. The mental model is uniform — every event
flows through the same `EventEnvelope` shape with the same ack
semantics regardless of broker.

---

## Mental model

```
                     ┌──────────────────────────┐
                     │     IEventPublisher      │
                     ├──────────────────────────┤
                     │ KafkaEventPublisher      │ ───► Kafka brokers
                     │ RabbitMqEventPublisher   │ ───► RabbitMQ broker
                     │ InMemoryEventPublisher   │ ───► Channel<T>
                     │ ResilientEventPublisher  │ ───► (wraps any of above)
                     └────────────┬─────────────┘
                                  │ PublishAsync(EventEnvelope)
                                  ▼
                     ┌──────────────────────────┐
                     │      EventEnvelope       │
                     │  Destination             │
                     │  EventType               │
                     │  Payload (any object)    │
                     │  Headers + Metadata      │
                     │  Ack callback            │
                     └──────────────────────────┘
                                  ▲
                                  │ ConsumeAsync → IAsyncEnumerable
                     ┌────────────┴─────────────┐
                     │      IEventConsumer      │
                     ├──────────────────────────┤
                     │ KafkaEventConsumer       │ ◄─── Kafka brokers
                     │ RabbitMqEventConsumer    │ ◄─── RabbitMQ broker
                     │ InMemoryEventConsumer    │ ◄─── Channel<T>
                     └──────────────────────────┘
```

The envelope flows end-to-end. The ack callback is the consumer's
responsibility — successful processing calls `ack.AcknowledgeAsync()`
(commit offset / basic.ack); failed processing calls
`ack.RejectAsync(error)` (Kafka: seek back to the offset; RabbitMQ:
basic.nack with requeue; in-memory: re-enqueue).

---

## Quick start

### Wire up

```csharp
using FireflyFramework.Eda.DependencyInjection;

builder.Services.AddFireflyEda(builder.Configuration);
```

`AddFireflyEda` reads `Firefly:Eda:DefaultPublisher` /
`DefaultConsumer` from configuration and registers the corresponding
publisher / consumer plus serialisers.

### Publish

```csharp
using FireflyFramework.Eda.Events;

public sealed class CreateOrderHandler : ICommandHandler<CreateOrder, Guid>
{
    public CreateOrderHandler(IEventPublisher publisher) { _publisher = publisher; }

    public async Task<Guid> HandleAsync(CreateOrder cmd, ExecutionContext ctx, CancellationToken ct)
    {
        var orderId = Guid.NewGuid();
        // ... persist the order ...

        var envelope = EventEnvelope
            .ForPublishing("orders.created", "OrderCreated", new { orderId, sku = cmd.Sku })
            .WithCorrelation(ctx.RequestId)
            .WithHeaders(new Dictionary<string, string>
            {
                ["tenant-id"] = ctx.TenantId ?? "default",
                ["user-id"]   = ctx.UserId ?? "anonymous",
            });

        await _publisher.PublishAsync(envelope, ct);
        return orderId;
    }
}
```

### Consume

```csharp
using FireflyFramework.Eda.Consumer;

public sealed class OrderProjectionWorker(IEventConsumer consumer, IOrderProjection projection) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await consumer.StartAsync(ct);

        await foreach (var envelope in consumer.ConsumeAsync(new[] { "orders.created" }, ct))
        {
            try
            {
                await projection.ApplyAsync(envelope.EventType, envelope.Payload, ct);
                if (envelope.Ack is { } ack) await ack.AcknowledgeAsync(ct);
            }
            catch (Exception ex)
            {
                if (envelope.Ack is { } ack) await ack.RejectAsync(ex, ct);
            }
        }
    }
}
```

---

## Public surface

### Publishers

| Type | Backing |
|---|---|
| `IEventPublisher` | Port: `Type`, `IsAvailable`, `PublishAsync`, `GetHealthAsync` |
| `KafkaEventPublisher` | Confluent.Kafka 2.6, idempotent producer, transactional support |
| `RabbitMqEventPublisher` | RabbitMQ.Client 7.x with publisher confirms |
| `InMemoryEventPublisher` | `Channel<T>` per destination via `InMemoryEventBus` |
| `ResilientEventPublisher` | Polly v8 wrapper: retry → circuit-breaker → timeout |

### Consumers

| Type | Backing |
|---|---|
| `IEventConsumer` | Port: `Type`, `IsRunning`, `IsAvailable`, `Start/Stop`, `ConsumeAsync` |
| `KafkaEventConsumer` | Manual offset commit; `KafkaAckCallback` commits on Acknowledge, seeks back on Reject — at-least-once |
| `RabbitMqEventConsumer` | `AsyncEventingBasicConsumer` with manual ack / nack |
| `InMemoryEventConsumer` | Reads from `InMemoryEventBus` channels |

### Serialisers

| Format | Class | Backing |
|---|---|---|
| JSON | `JsonMessageSerializer` | `System.Text.Json` |
| Protobuf | `ProtobufMessageSerializer` | `Google.Protobuf` |
| Avro | `AvroMessageSerializer` | `Apache.Avro` |
| Schema Registry Avro | `SchemaRegistryAvroSerializer<T>` | `Confluent.SchemaRegistry.Serdes.Avro` |
| Schema Registry Protobuf | `SchemaRegistryProtobufSerializer<T>` | `Confluent.SchemaRegistry.Serdes.Protobuf` |

Implement `IMessageSerializer` to add custom formats.

### Filters

Pre-delivery predicates composable into pipelines.

| Filter | Accepts when |
|---|---|
| `EventTypeFilter` | Event type matches a literal or `prefix.*` wildcard |
| `DestinationEventFilter` | Envelope destination is in the allowed set |
| `HeaderEventFilter` | Header is present (optionally with a specific value) |
| `CompositeEventFilter` | All child filters accept |

### Error handling

| Type | Purpose |
|---|---|
| `ErrorHandlingStrategy` | `Ignore`, `Retry`, `Halt`, `DeadLetter` |
| `IErrorHandler` | `HandleAsync(envelope, error, attempt)` returns a strategy |
| `DefaultErrorHandler` | Retries up to `MaxRetries`, then dead-letters |
| `MetricsErrorHandler` | Records error counts before delegating |
| `ChainErrorHandler` | Tries each handler in order; first non-Ignore wins |

### `EventEnvelope`

```csharp
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
    IAckCallback? Ack = null);
```

`EventMetadata` carries `CorrelationId`, `CausationId`, `Version`,
`Source`, `UserId`, `SessionId`, `TenantId`, plus an open `Custom`
dictionary. Static helpers:

* `EventEnvelope.ForPublishing(destination, eventType, payload)` —
  outbound envelope with default metadata.
* `.WithHeaders(...)`, `.WithCorrelation(...)`, `.WithMetadata(...)` —
  fluent decorators.

---

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
        "SchemaRegistryUrl": "http://localhost:8081",
        "EnableIdempotence": true,
        "Acks":              "All"
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

| Option | Effect |
|---|---|
| `DefaultPublisher` | One of `Kafka`, `RabbitMq`, `InMemory`. Picks which `IEventPublisher` is registered as the primary. |
| `DefaultConsumer` | Same enum for the consumer side. |
| `Kafka.BootstrapServers` | Comma-separated bootstrap list. |
| `Kafka.GroupId` | Consumer group; defaults to the service name. |
| `Kafka.SchemaRegistryUrl` | Required only when using `SchemaRegistry*Serializer`. |
| `Kafka.EnableIdempotence` | Idempotent producer (recommended in production). |
| `Kafka.Acks` | `None`, `Leader`, `All`. Use `All` for cross-broker durability. |
| `RabbitMq.*` | Standard RabbitMQ connection settings. |

---

## Common patterns

### Resilient publish

```csharp
var resilient = new ResilientEventPublisher(
    inner: kafkaPublisher,
    log: logger,
    options: new ResilientPublisherOptions
    {
        RetryAttempts = 5,
        RetryDelay    = TimeSpan.FromMilliseconds(100),
        BreakDuration = TimeSpan.FromSeconds(30),
        Timeout       = TimeSpan.FromSeconds(2),
    });
```

`ResilientEventPublisher` is itself an `IEventPublisher`, so it
substitutes anywhere the inner publisher does. The Polly v8
pipeline runs:

1. **Timeout** (default 2 s) — bounds the total publish call.
2. **Retry** (default 3 attempts, exponential backoff with jitter).
3. **Circuit breaker** (default 50 % failure rate over 20 calls).

### Filter chain on the consumer side

```csharp
var filter = new CompositeEventFilter(
    new EventTypeFilter("orders.*"),               // only order events
    new HeaderEventFilter("tenant-id", "alpha"));   // only tenant alpha

await foreach (var envelope in consumer.ConsumeAsync(topics, ct))
{
    if (!filter.Accept(envelope)) continue;
    // ... process
}
```

Filters are *pre-delivery*: rejected envelopes are still acked (or
nacked) according to the consumer's policy. They prevent expensive
handler execution for events that aren't relevant to this consumer
instance.

### Custom error handler chain

```csharp
var handler = new ChainErrorHandler(
    new MetricsErrorHandler(metrics),                 // record + delegate
    new DefaultErrorHandler(maxRetries: 5,            // retry then DLQ
                            deadLetterDestination: "orders.dlq"));
```

Each handler returns an `ErrorHandlingStrategy`; the first non-Ignore
strategy wins.

### Schema Registry on Kafka

```csharp
services.AddSingleton<IMessageSerializer<OrderCreated>>(sp =>
    new SchemaRegistryAvroSerializer<OrderCreated>(
        sp.GetRequiredService<ISchemaRegistryClient>()));
```

Schema-Registry serialisers handle subject naming, schema evolution
(forward / backward / full compatibility per the registry's policy),
and the Confluent wire format (one-byte magic + four-byte schema id +
payload).

### In-memory backbone for tests

```csharp
[Fact]
public async Task FireAndForget_PublishesOnInMemoryBus()
{
    var bus       = new InMemoryEventBus();
    var publisher = new InMemoryEventPublisher(bus);
    var consumer  = new InMemoryEventConsumer(bus);

    await publisher.PublishAsync(EventEnvelope.ForPublishing("test", "Ping", null));

    await foreach (var env in consumer.ConsumeAsync(new[] { "test" }, ct: cts.Token))
    {
        env.EventType.Should().Be("Ping");
        if (env.Ack is { } ack) await ack.AcknowledgeAsync();
        break;
    }
}
```

The in-memory bus is the same shape as Kafka / RabbitMQ but runs
entirely inside the process. Use it for unit / integration tests and
for single-host services that don't need durability.

---

## Pitfalls and gotchas

**Don't ignore the ack callback.** Forgetting `AcknowledgeAsync`
blocks the consumer group's offset on Kafka and leaves messages
"unacked" forever on RabbitMQ. The framework intentionally requires
explicit acks (no "auto-commit") so the handler decides when a
message is truly done.

**Don't ack before processing finishes.** A pre-emptive ack means
a handler crash mid-processing loses the event. Ack *after* the
handler's persistence has committed.

**Headers are strings.** Numeric metadata gets `.ToString()`'d on
publish and parsed on consume. If you need type fidelity, put the
data in `Payload` or in `EventMetadata.Custom`.

**Kafka's `seek back on Reject` is per-partition.** The
`KafkaAckCallback` rewinds the partition's offset on `RejectAsync`,
which retries the rejected message — but also every message that
came after it in the same partition. Pin one consumer per partition
or be ready to handle the redelivery cascade.

**RabbitMQ `RejectAsync` does `nack-with-requeue` by default.** That
puts the message back on the queue head, where it's the *next* thing
the same consumer pulls. You'll thrash unless you pair this with a
DLQ or a retry counter in the headers.

**`PublisherType.Auto` reads configuration.** Setting it explicitly
on a particular envelope (`PublisherType.Kafka`) overrides the default
publisher for that one publish — useful in mixed-broker deployments
but easy to forget.

**The default error handler dead-letters by destination, not by
event type.** A poison message that fails on every consumer goes to
`{originalDestination}.dlq`. Set up the DLQ topic / queue ahead of
time or you'll lose messages.

---

## Internals (for the curious)

`EventEnvelope` is a `record` so the framework can do
`envelope with { Headers = … }` cheaply. The `Headers` and
`EventMetadata` fields are nullable to keep the wire format compact —
no headers, no metadata bytes on the wire.

`KafkaEventConsumer.ConsumeAsync` uses `IAsyncEnumerable<EventEnvelope>`
because that's the natural shape for a long-running consumer loop.
The `await foreach` yields envelopes as they arrive; cancellation
on the outer token rolls back the in-flight commit. The
`KafkaAckCallback` is created once per yielded envelope.

`ResilientEventPublisher` is an *outer* wrapper, not an inner
delegating handler. It composes with any `IEventPublisher`, including
`InMemoryEventPublisher` (useful in tests that want to verify retry
behaviour without a real broker).

The Avro / Protobuf serialisers use the Confluent magic-byte format
on Kafka so the wire shape is identical to what a Java service
publishes via Confluent's libserdes. Plain Avro / Protobuf
(without Schema Registry) skips the magic-byte prefix.

Filters and error handlers are deliberately not run by the publisher.
The decision to publish or skip is the *handler's* responsibility, on
the consumer side. This keeps the publisher's hot path tight (a
single hash + send) and makes filter-vs-handler reasoning local.

---

## Dependencies

| Reference | Used for |
|---|---|
| `FireflyFramework.Kernel` (project) | Base exceptions |
| `Confluent.Kafka` (NuGet) | Kafka publisher / consumer |
| `Confluent.SchemaRegistry.Serdes.Avro` (NuGet) | Schema Registry Avro |
| `Confluent.SchemaRegistry.Serdes.Protobuf` (NuGet) | Schema Registry Protobuf |
| `RabbitMQ.Client` (NuGet) | RabbitMQ publisher / consumer |
| `Polly.Core` (NuGet) | Resilient publisher pipeline |
| `Google.Protobuf`, `Apache.Avro` (NuGet) | Bare-metal serialisation |

The Schema-Registry NuGets are loaded only when those serialisers
are instantiated; consumers using JSON-only payloads pay no Avro /
Protobuf cost.

---

## Java mapping

| .NET | Java |
|---|---|
| `IEventPublisher` | `EventPublisher` |
| `IEventConsumer` | `EventConsumer` |
| `EventEnvelope` | `EventEnvelope` |
| `KafkaEventPublisher` / `RabbitMqEventPublisher` / `InMemoryEventPublisher` | Same names |
| `ResilientEventPublisher` | `ResilientEventPublisher` |
| `IEventFilter` family | `EventFilter` + 4 implementations |
| `IErrorHandler` chain | `CustomErrorHandler` + registry |
| `IMessageSerializer<T>` | `MessageSerializer<T>` |
| `EventMetadata` | `EventMetadata` |

The wire shape (envelope JSON, header keys) matches byte-for-byte.
A `OrderCreated` event published from Java to Kafka is consumed
correctly by a .NET service in the same Kafka cluster, including
correlation, tenant, and metadata propagation.

---

## See also

* [`FireflyFramework.Cqrs`](../FireflyFramework.Cqrs/README.md) — `[PublishDomainEvent]` and `[InvalidateCacheOn]` integrate with this module.
* [`FireflyFramework.EventSourcing`](../FireflyFramework.EventSourcing/README.md) — emits domain events via this publisher.
* [`FireflyFramework.Orchestration`](../FireflyFramework.Orchestration/README.md) — saga / TCC engines publish lifecycle events.
* [`docs/CONFIGURATION.md`](../../docs/CONFIGURATION.md) — full `Firefly:Eda:*` reference.
