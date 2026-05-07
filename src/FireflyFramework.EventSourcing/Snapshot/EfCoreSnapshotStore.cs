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

using FireflyFramework.EventSourcing.Store.EfCore;
using Microsoft.EntityFrameworkCore;

namespace FireflyFramework.EventSourcing.Snapshot;

/// <summary>EF Core <see cref="ISnapshotStore"/>. Mirrors Java <c>R2dbcSnapshotStore</c>.</summary>
public sealed class EfCoreSnapshotStore : ISnapshotStore
{
    private readonly IDbContextFactory<EventStoreDbContext> _factory;

    public EfCoreSnapshotStore(IDbContextFactory<EventStoreDbContext> factory) => _factory = factory;

    public async Task SaveSnapshotAsync(AggregateSnapshot snapshot, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.Snapshots.Add(new SnapshotEntity
        {
            AggregateId = snapshot.AggregateId,
            SnapshotType = snapshot.SnapshotType,
            AggregateVersion = snapshot.AggregateVersion,
            Payload = snapshot.Payload,
            Timestamp = snapshot.Timestamp,
        });

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<AggregateSnapshot?> LoadLatestSnapshotAsync(Guid aggregateId, string snapshotType, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var row = await db.Snapshots
            .Where(s => s.AggregateId == aggregateId && s.SnapshotType == snapshotType)
            .OrderByDescending(s => s.AggregateVersion)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        return row is null ? null : new AggregateSnapshot(row.AggregateId, row.SnapshotType, row.AggregateVersion, row.Payload, row.Timestamp);
    }

    public async Task<AggregateSnapshot?> LoadSnapshotAtOrBeforeVersionAsync(Guid aggregateId, string snapshotType, long maxVersion, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var row = await db.Snapshots
            .Where(s => s.AggregateId == aggregateId && s.SnapshotType == snapshotType && s.AggregateVersion <= maxVersion)
            .OrderByDescending(s => s.AggregateVersion)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        return row is null ? null : new AggregateSnapshot(row.AggregateId, row.SnapshotType, row.AggregateVersion, row.Payload, row.Timestamp);
    }

    public async Task<bool> SnapshotExistsAsync(Guid aggregateId, string snapshotType, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Snapshots.AnyAsync(s => s.AggregateId == aggregateId && s.SnapshotType == snapshotType, ct).ConfigureAwait(false);
    }

    public async Task<long?> GetLatestSnapshotVersionAsync(Guid aggregateId, string snapshotType, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Snapshots
            .Where(s => s.AggregateId == aggregateId && s.SnapshotType == snapshotType)
            .OrderByDescending(s => s.AggregateVersion)
            .Select(s => (long?)s.AggregateVersion)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> KeepLatestSnapshotsAsync(Guid aggregateId, string snapshotType, int keep, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var ordered = await db.Snapshots
            .Where(s => s.AggregateId == aggregateId && s.SnapshotType == snapshotType)
            .OrderByDescending(s => s.AggregateVersion)
            .Skip(keep)
            .ToListAsync(ct).ConfigureAwait(false);

        db.Snapshots.RemoveRange(ordered);
        return await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
