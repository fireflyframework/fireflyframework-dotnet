using System.Threading.Channels;
using FireflyFramework.Eda.Events;

namespace FireflyFramework.Eda.Publisher;

/// <summary>
/// In-memory publisher backed by a <see cref="Channel{T}"/> per destination. Useful for
/// tests and for the in-process Spring-Application-Event analogue.
/// </summary>
public sealed class InMemoryEventPublisher : IEventPublisher
{
    private readonly InMemoryEventBus _bus;

    public InMemoryEventPublisher(InMemoryEventBus bus) => _bus = bus;

    public PublisherType Type => PublisherType.InMemory;
    public string? DefaultDestination => null;
    public bool IsAvailable => true;

    public Task PublishAsync(EventEnvelope envelope, CancellationToken ct = default) =>
        _bus.PublishAsync(envelope, ct);

    public Task<PublisherHealth> GetHealthAsync(CancellationToken ct = default) =>
        Task.FromResult(new PublisherHealth(Type, true, "UP"));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>Per-process pub/sub backbone — used by both the in-memory publisher and consumer.</summary>
public sealed class InMemoryEventBus
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Channel<EventEnvelope>> _channels = new();

    public Channel<EventEnvelope> Channel(string destination) =>
        _channels.GetOrAdd(destination, _ => System.Threading.Channels.Channel.CreateUnbounded<EventEnvelope>());

    public async Task PublishAsync(EventEnvelope envelope, CancellationToken ct = default) =>
        await Channel(envelope.Destination).Writer.WriteAsync(envelope, ct).ConfigureAwait(false);
}
