using System.Reflection;
using FireflyFramework.EventSourcing.Annotations;

namespace FireflyFramework.EventSourcing.Domain;

/// <summary>Domain event contract. Mirrors Java <c>Event</c>.</summary>
public interface IDomainEvent
{
    Guid AggregateId { get; }
    DateTimeOffset Timestamp { get; }

    string EventType => GetType().GetCustomAttribute<DomainEventAttribute>()?.EventType ?? GetType().Name;

    int EventVersion => GetType().GetCustomAttribute<DomainEventAttribute>()?.Version ?? 1;

    Dictionary<string, object?>? Metadata => null;
}

public abstract record AbstractDomainEvent(Guid AggregateId, DateTimeOffset Timestamp) : IDomainEvent;
