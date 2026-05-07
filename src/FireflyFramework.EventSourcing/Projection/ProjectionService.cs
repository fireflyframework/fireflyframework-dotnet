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

using FireflyFramework.EventSourcing.Store;
using FireflyFramework.EventSourcing.Upcasting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FireflyFramework.EventSourcing.Projection;

/// <summary>
/// A projection consumes events from the store and updates a read model. Each
/// projection is identified by a stable name and tracks its last-processed
/// <c>globalSequence</c> via <see cref="IProjectionCheckpointStore"/>.
/// Mirrors Java <c>ProjectionService</c> + <c>ProjectionProcessor</c>.
/// </summary>
public interface IProjection
{
    string Name { get; }
    Task ApplyAsync(StoredEventEnvelope envelope, CancellationToken ct = default);
}

public interface IProjectionCheckpointStore
{
    Task<long> GetLastProcessedAsync(string projectionName, CancellationToken ct = default);
    Task SaveCheckpointAsync(string projectionName, long globalSequence, CancellationToken ct = default);
}

/// <summary>In-memory checkpoint store. Replace with EF Core for production.</summary>
public sealed class InMemoryProjectionCheckpointStore : IProjectionCheckpointStore
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> _checkpoints = new();

    public Task<long> GetLastProcessedAsync(string projectionName, CancellationToken ct = default) =>
        Task.FromResult(_checkpoints.TryGetValue(projectionName, out var c) ? c : 0L);

    public Task SaveCheckpointAsync(string projectionName, long globalSequence, CancellationToken ct = default)
    {
        _checkpoints[projectionName] = globalSequence;
        return Task.CompletedTask;
    }
}

/// <summary>
/// <see cref="IHostedService"/> that drives one or more <see cref="IProjection"/>s by
/// polling the event store for new events from the last checkpoint, applying them,
/// then persisting the new checkpoint.
/// </summary>
public sealed class ProjectionRunner : BackgroundService
{
    private readonly IEventStore _store;
    private readonly IEnumerable<IProjection> _projections;
    private readonly IProjectionCheckpointStore _checkpoints;
    private readonly EventUpcastingService? _upcaster;
    private readonly ILogger<ProjectionRunner> _log;
    private readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(500);

    public ProjectionRunner(
        IEventStore store,
        IEnumerable<IProjection> projections,
        IProjectionCheckpointStore checkpoints,
        ILogger<ProjectionRunner> log,
        EventUpcastingService? upcaster = null)
    {
        _store = store;
        _projections = projections.ToList();
        _checkpoints = checkpoints;
        _log = log;
        _upcaster = upcaster;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var projection in _projections)
            {
                try
                {
                    var checkpoint = await _checkpoints.GetLastProcessedAsync(projection.Name, stoppingToken).ConfigureAwait(false);
                    var max = checkpoint;

                    await foreach (var envelope in _store.StreamAllEventsFromAsync(checkpoint, stoppingToken).ConfigureAwait(false))
                    {
                        var toApply = _upcaster is null ? envelope : _upcaster.Apply(envelope);
                        await projection.ApplyAsync(toApply, stoppingToken).ConfigureAwait(false);
                        max = toApply.GlobalSequence;
                    }

                    if (max > checkpoint)
                    {
                        await _checkpoints.SaveCheckpointAsync(projection.Name, max, stoppingToken).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Projection {Projection} run failed", projection.Name);
                }
            }

            await Task.Delay(_pollInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
