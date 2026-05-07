using FireflyFramework.Orchestration.Core;

namespace FireflyFramework.Orchestration.Persistence;

/// <summary>SPI for persisting orchestration state. Mirrors Java <c>ExecutionPersistenceProvider</c>.</summary>
public interface IExecutionPersistenceProvider
{
    Task SaveAsync(OrchestrationExecutionContext state, CancellationToken ct = default);
    Task<OrchestrationExecutionContext?> FindByIdAsync(string correlationId, CancellationToken ct = default);
    Task UpdateStatusAsync(string correlationId, ExecutionStatus status, CancellationToken ct = default);
    IAsyncEnumerable<OrchestrationExecutionContext> FindByPatternAsync(ExecutionPattern pattern, CancellationToken ct = default);
    IAsyncEnumerable<OrchestrationExecutionContext> FindByStatusAsync(ExecutionStatus status, CancellationToken ct = default);
    IAsyncEnumerable<OrchestrationExecutionContext> FindInFlightAsync(CancellationToken ct = default);
    Task<int> CleanupAsync(TimeSpan olderThan, CancellationToken ct = default);
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}
