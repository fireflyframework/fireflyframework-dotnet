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
using Microsoft.Extensions.Logging;

namespace FireflyFramework.Eda.ErrorHandling;

/// <summary>
/// What to do with an event that the listener could not process. Mirrors Java
/// <c>ErrorHandlingStrategy</c>.
/// </summary>
public enum ErrorHandlingStrategy
{
    /// <summary>Acknowledge and drop the message.</summary>
    Ignore,
    /// <summary>Reject the message; the broker will redeliver based on its retry policy.</summary>
    Retry,
    /// <summary>Reject the message and stop the consumer.</summary>
    Halt,
    /// <summary>Push the message into the configured dead-letter queue.</summary>
    DeadLetter,
}

/// <summary>
/// Pluggable error handler invoked when a listener throws. Mirrors Java
/// <c>CustomErrorHandler</c>.
/// </summary>
public interface IErrorHandler
{
    Task<ErrorHandlingStrategy> HandleAsync(EventEnvelope envelope, Exception error, int attempt, CancellationToken ct = default);
}

/// <summary>Logs and retries up to <see cref="MaxRetries"/>, then dead-letters.</summary>
public sealed class DefaultErrorHandler : IErrorHandler
{
    public int MaxRetries { get; init; } = 3;
    private readonly ILogger<DefaultErrorHandler> _log;

    public DefaultErrorHandler(ILogger<DefaultErrorHandler> log) => _log = log;

    public Task<ErrorHandlingStrategy> HandleAsync(EventEnvelope envelope, Exception error, int attempt, CancellationToken ct = default)
    {
        if (attempt < MaxRetries)
        {
            _log.LogWarning(error, "Listener failed for {EventType} (attempt {Attempt}/{Max}); retrying", envelope.EventType, attempt, MaxRetries);
            return Task.FromResult(ErrorHandlingStrategy.Retry);
        }

        _log.LogError(error, "Listener exhausted retries for {EventType}; dead-lettering", envelope.EventType);
        return Task.FromResult(ErrorHandlingStrategy.DeadLetter);
    }
}

/// <summary>
/// Records error counts and timings against a metric. Plug into
/// <see cref="System.Diagnostics.Metrics"/> from your service. Mirrors Java
/// <c>MetricsErrorHandler</c>.
/// </summary>
public sealed class MetricsErrorHandler : IErrorHandler
{
    private readonly Action<EventEnvelope, Exception> _record;
    private readonly IErrorHandler _inner;

    public MetricsErrorHandler(IErrorHandler inner, Action<EventEnvelope, Exception> record)
    {
        _inner = inner;
        _record = record;
    }

    public Task<ErrorHandlingStrategy> HandleAsync(EventEnvelope envelope, Exception error, int attempt, CancellationToken ct = default)
    {
        _record(envelope, error);
        return _inner.HandleAsync(envelope, error, attempt, ct);
    }
}

/// <summary>Tries each handler in order; first non-Ignore decision wins. Mirrors Java <c>CustomErrorHandlerRegistry</c>.</summary>
public sealed class ChainErrorHandler : IErrorHandler
{
    private readonly IReadOnlyList<IErrorHandler> _handlers;

    public ChainErrorHandler(IEnumerable<IErrorHandler> handlers) => _handlers = handlers.ToList();

    public async Task<ErrorHandlingStrategy> HandleAsync(EventEnvelope envelope, Exception error, int attempt, CancellationToken ct = default)
    {
        foreach (var h in _handlers)
        {
            var decision = await h.HandleAsync(envelope, error, attempt, ct).ConfigureAwait(false);
            if (decision != ErrorHandlingStrategy.Ignore) return decision;
        }

        return ErrorHandlingStrategy.Ignore;
    }
}
