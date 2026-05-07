using FireflyFramework.Eda.Events;

namespace FireflyFramework.Eda.Publisher;

/// <summary>
/// Unified async publisher contract. Mirrors Java <c>EventPublisher</c>: implementations
/// exist for Kafka, RabbitMQ and an in-memory test double.
/// </summary>
public interface IEventPublisher : IAsyncDisposable
{
    PublisherType Type { get; }
    string? DefaultDestination { get; }
    bool IsAvailable { get; }

    Task PublishAsync(EventEnvelope envelope, CancellationToken ct = default);
    Task<PublisherHealth> GetHealthAsync(CancellationToken ct = default);
}

public sealed record PublisherHealth(PublisherType Type, bool Available, string Status, string? Detail = null);
