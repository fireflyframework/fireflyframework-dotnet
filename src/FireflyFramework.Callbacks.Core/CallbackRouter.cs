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

using FireflyFramework.Callbacks.Interfaces;
using FireflyFramework.Callbacks.Models;
using Microsoft.Extensions.Logging;

namespace FireflyFramework.Callbacks.Core;

/// <summary>
/// Receives a domain event and dispatches it to every subscribed callback. Mirrors
/// Java <c>CallbackRouter</c>.
/// </summary>
public interface ICallbackRouter
{
    Task<IReadOnlyList<CallbackExecutionDto>> RouteAsync(string eventType, string payload, string? tenantId = null, CancellationToken ct = default);
}

public sealed class CallbackRouter : ICallbackRouter
{
    private readonly ICallbackConfigurationStore _store;
    private readonly ICallbackDispatcher _dispatcher;
    private readonly ICallbackExecutionStore? _executions;
    private readonly IDomainAuthorizationService? _domainAuth;
    private readonly ILogger<CallbackRouter> _log;

    public CallbackRouter(
        ICallbackConfigurationStore store,
        ICallbackDispatcher dispatcher,
        ILogger<CallbackRouter> log,
        ICallbackExecutionStore? executions = null,
        IDomainAuthorizationService? domainAuth = null)
    {
        _store = store;
        _dispatcher = dispatcher;
        _log = log;
        _executions = executions;
        _domainAuth = domainAuth;
    }

    public async Task<IReadOnlyList<CallbackExecutionDto>> RouteAsync(
        string eventType, string payload, string? tenantId = null, CancellationToken ct = default)
    {
        var subscribers = await _store.FindBySubscribedEventAsync(eventType, tenantId, ct).ConfigureAwait(false);
        if (subscribers.Count == 0)
        {
            _log.LogDebug("No callbacks subscribed to {EventType}", eventType);
            return Array.Empty<CallbackExecutionDto>();
        }

        var results = new List<CallbackExecutionDto>(subscribers.Count);
        foreach (var config in subscribers)
        {
            if (_domainAuth is not null && !await _domainAuth.IsAuthorizedAsync(config.Url, ct).ConfigureAwait(false))
            {
                _log.LogWarning("Skipping callback {Id}: domain {Url} is not authorized", config.Id, config.Url);
                continue;
            }

            var entity = ToEntity(config);
            var execution = await _dispatcher.DispatchAsync(entity, eventType, payload, ct).ConfigureAwait(false);
            results.Add(execution);

            if (_executions is not null)
            {
                await _executions.RecordAsync(execution, ct).ConfigureAwait(false);
            }
        }

        return results;
    }

    private static CallbackConfigurationEntity ToEntity(CallbackConfigurationDto dto) => new()
    {
        Id = dto.Id ?? Guid.NewGuid(),
        Name = dto.Name,
        Url = dto.Url,
        HttpMethod = dto.HttpMethod,
        Status = dto.Status,
        Secret = dto.Secret,
        SignatureEnabled = dto.SignatureEnabled,
        SignatureHeader = dto.SignatureHeader,
        MaxRetries = dto.MaxRetries,
        RetryDelayMs = dto.RetryDelayMs,
        TimeoutMs = dto.TimeoutMs,
    };
}
