// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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
