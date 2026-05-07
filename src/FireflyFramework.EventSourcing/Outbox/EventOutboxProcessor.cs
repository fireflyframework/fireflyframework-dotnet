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
using FireflyFramework.EventSourcing.Store.EfCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FireflyFramework.EventSourcing.Outbox;

/// <summary>
/// Drains the transactional event outbox to the EDA bus. Mirrors Java
/// <c>EventOutboxProcessor</c>.
/// </summary>
/// <remarks>
/// Polls the <c>firefly_event_outbox</c> table for unpublished rows, publishes each one
/// through the configured <see cref="IEventPublisher"/>, and marks it as published in
/// the same transaction. Designed for at-least-once delivery — duplicate handling is
/// the consumer's responsibility (use <see cref="EventEnvelope.Metadata"/> for idempotency).
/// </remarks>
public sealed class EventOutboxProcessor : BackgroundService
{
    private readonly IDbContextFactory<EventStoreDbContext> _factory;
    private readonly IEventPublisher _publisher;
    private readonly ILogger<EventOutboxProcessor> _log;
    private readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(500);
    private readonly int _batchSize = 100;

    public EventOutboxProcessor(
        IDbContextFactory<EventStoreDbContext> factory,
        IEventPublisher publisher,
        ILogger<EventOutboxProcessor> log)
    {
        _factory = factory;
        _publisher = publisher;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Outbox processor poll failed");
            }

            await Task.Delay(_pollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var pending = await db.Outbox
            .Where(o => !o.Published)
            .OrderBy(o => o.CreatedAt)
            .Take(_batchSize)
            .ToListAsync(ct).ConfigureAwait(false);

        foreach (var entry in pending)
        {
            try
            {
                var envelope = EventEnvelope.ForPublishing(entry.Destination, entry.EventType, entry.Payload);
                await _publisher.PublishAsync(envelope, ct).ConfigureAwait(false);
                entry.Published = true;
                entry.PublishedAt = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to publish outbox entry {Id}", entry.Id);
            }
        }

        if (pending.Count > 0)
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}
