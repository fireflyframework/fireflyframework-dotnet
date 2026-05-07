using FireflyFramework.Eda.Events;
using FireflyFramework.Eda.Publisher;
using FireflyFramework.EventSourcing.Domain;
using Microsoft.Extensions.Logging;

namespace FireflyFramework.EventSourcing.Publisher;

/// <summary>
/// Convenience helper for publishing aggregate events directly to the EDA bus, e.g.
/// inside a domain service that doesn't yet sit behind the outbox processor. Mirrors
/// Java <c>EventSourcingPublisher</c>.
/// </summary>
public sealed class EventSourcingPublisher
{
    private readonly IEventPublisher _publisher;
    private readonly ILogger<EventSourcingPublisher> _log;

    public EventSourcingPublisher(IEventPublisher publisher, ILogger<EventSourcingPublisher> log)
    {
        _publisher = publisher;
        _log = log;
    }

    public async Task PublishAsync(IDomainEvent @event, string destination, CancellationToken ct = default)
    {
        var envelope = EventEnvelope.ForPublishing(destination, @event.EventType, @event)
            .WithMetadata(new EventMetadata(
                CorrelationId: @event.AggregateId.ToString("N"),
                Version: @event.EventVersion.ToString()));

        await _publisher.PublishAsync(envelope, ct).ConfigureAwait(false);
        _log.LogDebug("Published domain event {EventType} for aggregate {AggregateId}", @event.EventType, @event.AggregateId);
    }
}
