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
