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

    /// <summary>
    /// Streams executions that are still "in-flight" (Running / Waiting / Suspended) and were
    /// last seen before <paramref name="threshold"/> — used by <c>RecoveryService</c> to find
    /// orphaned executions whose owning host has crashed or restarted.
    /// </summary>
    IAsyncEnumerable<OrchestrationExecutionContext> FindStaleAsync(DateTimeOffset threshold, CancellationToken ct = default);

    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}
