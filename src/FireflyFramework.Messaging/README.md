# FireflyFramework.Messaging

Lightweight Spring Messaging port. Provides the `Message<T>` envelope,
`IMessageBroker` send/subscribe contract, and `[MessageListener]`
attribute used by callers that don't need the full EDA stack
(serialization, schema registry, DLQ, circuit breakers).

## When to use this vs `FireflyFramework.Eda`

| Need | Module |
|---|---|
| In-process pub/sub between services in the same host | **Messaging** |
| Cross-process Kafka/RabbitMQ event bus, schema registry, DLQ | **Eda** |
| Distributed transactions (saga, workflow, TCC) | **Orchestration** |

## Quick start

```csharp
services.AddFireflyMessaging();

public sealed class WelcomeMailer
{
    public WelcomeMailer(IMessageBroker broker)
    {
        broker.Subscribe<UserSignedUp>("users.signed-up", HandleAsync);
    }

    private Task HandleAsync(Message<UserSignedUp> m, CancellationToken ct)
    {
        ...
    }
}

await broker.SendAsync("users.signed-up", Message<UserSignedUp>.Of(evt));
```
