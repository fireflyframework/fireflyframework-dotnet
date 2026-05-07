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
