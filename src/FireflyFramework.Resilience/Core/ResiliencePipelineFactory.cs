// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Threading.RateLimiting;
using FireflyFramework.Resilience.Configuration;
using Polly;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Retry;
using Polly.Timeout;

namespace FireflyFramework.Resilience.Core;

/// <summary>Builds Polly pipelines from declarative <see cref="FireflyResilienceOptions"/>.</summary>
public static class ResiliencePipelineFactory
{
    public static ResiliencePipeline BuildCircuitBreaker(string name, CircuitBreakerOptions o) =>
        new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                Name = name,
                FailureRatio = o.FailureRateThreshold,
                SamplingDuration = o.SamplingDuration,
                MinimumThroughput = o.MinimumThroughput,
                BreakDuration = o.BreakDuration,
            })
            .Build();

    public static ResiliencePipeline BuildRetry(string name, RetryOptions o) =>
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                Name = name,
                MaxRetryAttempts = o.MaxAttempts,
                Delay = o.Delay,
                BackoffType = Enum.TryParse<DelayBackoffType>(o.BackoffType, true, out var b) ? b : DelayBackoffType.Exponential,
                UseJitter = o.UseJitter,
            })
            .Build();

    public static ResiliencePipeline BuildRateLimiter(string name, Configuration.RateLimiterOptions o) =>
        new ResiliencePipelineBuilder()
            .AddRateLimiter(new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
            {
                PermitLimit = o.PermitLimit,
                Window = o.Window,
                SegmentsPerWindow = 4,
                QueueLimit = o.QueueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            }))
            .Build();

    public static ResiliencePipeline BuildBulkhead(string name, Configuration.BulkheadOptions o) =>
        new ResiliencePipelineBuilder()
            .AddConcurrencyLimiter(new ConcurrencyLimiterOptions
            {
                PermitLimit = o.MaxConcurrency,
                QueueLimit = o.MaxQueue,
            })
            .Build();

    public static ResiliencePipeline BuildTimeLimiter(string name, Configuration.TimeLimiterOptions o) =>
        new ResiliencePipelineBuilder()
            .AddTimeout(new TimeoutStrategyOptions { Name = name, Timeout = o.Timeout })
            .Build();
}
