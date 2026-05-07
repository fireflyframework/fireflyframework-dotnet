using FireflyFramework.Orchestration.Core;
using FireflyFramework.Orchestration.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FireflyFramework.Orchestration.Recovery;

/// <summary>
/// Recovers orphaned executions whose owning host crashed mid-flight. Mirrors Java
/// <c>RecoveryService</c>. Two responsibilities:
///
/// <list type="number">
/// <item>List executions that have been "in-flight" longer than <see cref="StaleThreshold"/>
/// (Running / Waiting / Suspended) so a coordinator can resume them.</item>
/// <item>Reap completed executions older than a caller-supplied retention window so the
/// persistence store doesn't grow unbounded.</item>
/// </list>
///
/// <para>The recovery loop itself is not started automatically — callers are expected to
/// schedule <see cref="FindStaleAsync"/> + <see cref="CleanupCompletedAsync"/> from a hosted
/// background service or a cron job.</para>
/// </summary>
public sealed class RecoveryService
{
    private readonly IExecutionPersistenceProvider _persistence;
    private readonly ILogger<RecoveryService> _logger;

    /// <summary>
    /// How long an in-flight execution may go without a heartbeat before it's considered
    /// stale. Defaults to 5 minutes.
    /// </summary>
    public TimeSpan StaleThreshold { get; init; } = TimeSpan.FromMinutes(5);

    public RecoveryService(IExecutionPersistenceProvider persistence, ILogger<RecoveryService>? logger = null)
    {
        _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        _logger = logger ?? NullLogger<RecoveryService>.Instance;
    }

    /// <summary>
    /// Streams every in-flight execution older than <see cref="StaleThreshold"/>. Caller
    /// decides what to do with them — typically: re-load into the engine, increment an
    /// attempt counter, and re-execute from the last successful step.
    /// </summary>
    public IAsyncEnumerable<OrchestrationExecutionContext> FindStaleAsync(CancellationToken ct = default)
    {
        var threshold = DateTimeOffset.UtcNow - StaleThreshold;
        _logger.LogDebug("[recovery] scanning for executions stale before {Threshold}", threshold);
        return _persistence.FindStaleAsync(threshold, ct);
    }

    /// <summary>
    /// Removes every completed execution older than <paramref name="olderThan"/>. Returns
    /// the number of records reaped. Surfaces persistence errors to the caller.
    /// </summary>
    public async Task<int> CleanupCompletedAsync(TimeSpan olderThan, CancellationToken ct = default)
    {
        if (olderThan <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(olderThan), "must be positive");
        try
        {
            var count = await _persistence.CleanupAsync(olderThan, ct).ConfigureAwait(false);
            _logger.LogInformation("[recovery] cleaned up {Count} completed executions older than {OlderThan}", count, olderThan);
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[recovery] cleanup failed for {OlderThan}", olderThan);
            throw;
        }
    }
}
