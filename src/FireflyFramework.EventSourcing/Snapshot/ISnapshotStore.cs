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

namespace FireflyFramework.EventSourcing.Snapshot;

public sealed record AggregateSnapshot(
    Guid AggregateId,
    string SnapshotType,
    long AggregateVersion,
    string Payload,
    DateTimeOffset Timestamp);

public interface ISnapshotStore
{
    Task SaveSnapshotAsync(AggregateSnapshot snapshot, CancellationToken ct = default);
    Task<AggregateSnapshot?> LoadLatestSnapshotAsync(Guid aggregateId, string snapshotType, CancellationToken ct = default);
    Task<AggregateSnapshot?> LoadSnapshotAtOrBeforeVersionAsync(Guid aggregateId, string snapshotType, long maxVersion, CancellationToken ct = default);
    Task<bool> SnapshotExistsAsync(Guid aggregateId, string snapshotType, CancellationToken ct = default);
    Task<long?> GetLatestSnapshotVersionAsync(Guid aggregateId, string snapshotType, CancellationToken ct = default);
    Task<int> KeepLatestSnapshotsAsync(Guid aggregateId, string snapshotType, int keep, CancellationToken ct = default);
}
