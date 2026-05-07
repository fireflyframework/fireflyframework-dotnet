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

using System.Collections.Concurrent;
using FireflyFramework.Callbacks.Interfaces;

namespace FireflyFramework.Callbacks.Core;

/// <summary>
/// Persists callback executions for replay, audit, and dashboards. Mirrors Java
/// <c>CallbackExecutionRepository</c>.
/// </summary>
public interface ICallbackExecutionStore
{
    Task RecordAsync(CallbackExecutionDto execution, CancellationToken ct = default);
    Task<IReadOnlyList<CallbackExecutionDto>> ListByConfigurationAsync(Guid configurationId, int limit = 100, CancellationToken ct = default);
    Task<IReadOnlyList<CallbackExecutionDto>> ListByStatusAsync(CallbackExecutionStatus status, int limit = 100, CancellationToken ct = default);
    Task<CallbackExecutionDto?> GetAsync(Guid id, CancellationToken ct = default);
}

public sealed class InMemoryCallbackExecutionStore : ICallbackExecutionStore
{
    private readonly ConcurrentBag<CallbackExecutionDto> _all = new();

    public Task RecordAsync(CallbackExecutionDto execution, CancellationToken ct = default)
    {
        _all.Add(execution);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CallbackExecutionDto>> ListByConfigurationAsync(Guid configurationId, int limit = 100, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CallbackExecutionDto>>(_all
            .Where(e => e.ConfigurationId == configurationId)
            .OrderByDescending(e => e.ExecutedAt)
            .Take(limit)
            .ToList());

    public Task<IReadOnlyList<CallbackExecutionDto>> ListByStatusAsync(CallbackExecutionStatus status, int limit = 100, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CallbackExecutionDto>>(_all
            .Where(e => e.Status == status)
            .OrderByDescending(e => e.ExecutedAt)
            .Take(limit)
            .ToList());

    public Task<CallbackExecutionDto?> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_all.FirstOrDefault(e => e.Id == id));
}
