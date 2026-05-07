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
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace FireflyFramework.Eda.Publisher;

/// <summary>
/// Wraps an underlying <see cref="IEventPublisher"/> with a Polly resilience pipeline:
/// retry → circuit-breaker → timeout. Mirrors Java <c>ResilientEventPublisher</c>.
/// </summary>
public sealed class ResilientEventPublisher : IEventPublisher
{
    private readonly IEventPublisher _inner;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<ResilientEventPublisher> _log;

    public ResilientEventPublisher(
        IEventPublisher inner,
        ILogger<ResilientEventPublisher> log,
        ResilientPublisherOptions? options = null)
    {
        _inner = inner;
        _log = log;
        var opts = options ?? new ResilientPublisherOptions();

        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = opts.RetryAttempts,
                Delay = opts.RetryDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = opts.FailureRatio,
                MinimumThroughput = opts.MinimumThroughput,
                SamplingDuration = opts.SamplingDuration,
                BreakDuration = opts.BreakDuration,
            })
            .AddTimeout(new TimeoutStrategyOptions { Timeout = opts.Timeout })
            .Build();
    }

    public PublisherType Type => _inner.Type;
    public string? DefaultDestination => _inner.DefaultDestination;
    public bool IsAvailable => _inner.IsAvailable;

    public Task PublishAsync(EventEnvelope envelope, CancellationToken ct = default) =>
        _pipeline.ExecuteAsync(async cancel => await _inner.PublishAsync(envelope, cancel).ConfigureAwait(false), ct).AsTask();

    public Task<PublisherHealth> GetHealthAsync(CancellationToken ct = default) =>
        _inner.GetHealthAsync(ct);

    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}

public sealed class ResilientPublisherOptions
{
    public int RetryAttempts { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(200);
    public double FailureRatio { get; set; } = 0.5;
    public int MinimumThroughput { get; set; } = 10;
    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(15);
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}
