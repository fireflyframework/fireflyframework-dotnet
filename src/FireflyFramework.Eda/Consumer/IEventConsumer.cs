using FireflyFramework.Eda.Events;

namespace FireflyFramework.Eda.Consumer;

/// <summary>Unified async consumer contract. Mirrors Java <c>EventConsumer</c>.</summary>
public interface IEventConsumer : IAsyncDisposable
{
    ConsumerType Type { get; }
    bool IsRunning { get; }
    bool IsAvailable { get; }

    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);

    /// <summary>Streams events from one or more destinations as an async sequence.</summary>
    IAsyncEnumerable<EventEnvelope> ConsumeAsync(IEnumerable<string> destinations, CancellationToken ct = default);

    Task<ConsumerHealth> GetHealthAsync(CancellationToken ct = default);
}

public sealed record ConsumerHealth(ConsumerType Type, bool Available, bool Running, string Status);
