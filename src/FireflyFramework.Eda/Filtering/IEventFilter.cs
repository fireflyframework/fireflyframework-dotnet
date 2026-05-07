using FireflyFramework.Eda.Events;

namespace FireflyFramework.Eda.Filtering;

/// <summary>
/// Predicate over an <see cref="EventEnvelope"/> applied before delivery to a listener.
/// Mirrors Java <c>EventFilter</c>.
/// </summary>
public interface IEventFilter
{
    bool Accepts(EventEnvelope envelope);
}

/// <summary>Accepts the envelope iff every child filter accepts it. Mirrors Java <c>CompositeEventFilter</c>.</summary>
public sealed class CompositeEventFilter : IEventFilter
{
    private readonly IReadOnlyList<IEventFilter> _filters;
    public CompositeEventFilter(IEnumerable<IEventFilter> filters) => _filters = filters.ToList();
    public bool Accepts(EventEnvelope envelope) => _filters.All(f => f.Accepts(envelope));
}

/// <summary>Filters by event-type literal or wildcard pattern. Mirrors Java <c>EventTypeFilter</c>.</summary>
public sealed class EventTypeFilter : IEventFilter
{
    private readonly Func<string, bool> _match;

    public EventTypeFilter(string typePattern)
    {
        if (typePattern.EndsWith("*", StringComparison.Ordinal))
        {
            var prefix = typePattern[..^1];
            _match = type => type.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            _match = type => string.Equals(type, typePattern, StringComparison.OrdinalIgnoreCase);
        }
    }

    public bool Accepts(EventEnvelope envelope) => _match(envelope.EventType);
}

/// <summary>Filters by destination (topic / queue). Mirrors Java <c>DestinationEventFilter</c>.</summary>
public sealed class DestinationEventFilter : IEventFilter
{
    private readonly HashSet<string> _allowed;
    public DestinationEventFilter(IEnumerable<string> destinations) =>
        _allowed = new HashSet<string>(destinations, StringComparer.OrdinalIgnoreCase);
    public bool Accepts(EventEnvelope envelope) => _allowed.Contains(envelope.Destination);
}

/// <summary>Filters by header presence/value. Mirrors Java <c>HeaderEventFilter</c>.</summary>
public sealed class HeaderEventFilter : IEventFilter
{
    private readonly string _header;
    private readonly string? _value;

    public HeaderEventFilter(string header, string? value = null)
    {
        _header = header;
        _value = value;
    }

    public bool Accepts(EventEnvelope envelope)
    {
        if (envelope.Headers is null || !envelope.Headers.TryGetValue(_header, out var v))
        {
            return false;
        }

        return _value is null || string.Equals(v, _value, StringComparison.OrdinalIgnoreCase);
    }
}
