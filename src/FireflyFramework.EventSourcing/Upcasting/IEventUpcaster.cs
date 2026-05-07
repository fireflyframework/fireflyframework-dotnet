using FireflyFramework.EventSourcing.Store;

namespace FireflyFramework.EventSourcing.Upcasting;

/// <summary>
/// Migrates a stored event payload from one schema version to the next as the
/// aggregate's event shape evolves over time. Mirrors Java <c>EventUpcaster</c>.
/// </summary>
public interface IEventUpcaster
{
    string EventType { get; }
    int FromVersion { get; }
    int ToVersion { get; }
    StoredEventEnvelope Upcast(StoredEventEnvelope envelope);
}

/// <summary>
/// Pipeline that runs every applicable upcaster for an event in the right order
/// (FromVersion → ToVersion → ...) until the event reaches the latest schema.
/// </summary>
public sealed class EventUpcastingService
{
    private readonly IReadOnlyList<IEventUpcaster> _upcasters;

    public EventUpcastingService(IEnumerable<IEventUpcaster> upcasters)
    {
        _upcasters = upcasters.ToList();
    }

    public StoredEventEnvelope Apply(StoredEventEnvelope envelope)
    {
        var current = envelope;
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var upcaster in _upcasters)
            {
                if (upcaster.EventType == current.EventType && upcaster.FromVersion == current.EventVersion)
                {
                    current = upcaster.Upcast(current);
                    changed = true;
                    break;
                }
            }
        }

        return current;
    }
}
