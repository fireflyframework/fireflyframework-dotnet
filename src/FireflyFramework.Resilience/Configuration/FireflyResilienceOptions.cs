// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.Resilience.Configuration;

/// <summary>
/// Configuration root for the resilience module. Mirrors Java
/// <c>Resilience4jProperties</c>: every dictionary maps a logical name to a
/// strategy-specific options bag, and the registry produces one Polly pipeline
/// per (strategy, name) pair.
/// </summary>
public sealed class FireflyResilienceOptions
{
    public const string SectionName = "Firefly:Resilience";

    /// <summary>Named circuit breakers keyed by registry name.</summary>
    public IDictionary<string, CircuitBreakerOptions> CircuitBreakers { get; set; } = new Dictionary<string, CircuitBreakerOptions>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Named retry strategies keyed by registry name.</summary>
    public IDictionary<string, RetryOptions> Retries { get; set; } = new Dictionary<string, RetryOptions>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Named sliding-window rate limiters keyed by registry name.</summary>
    public IDictionary<string, RateLimiterOptions> RateLimiters { get; set; } = new Dictionary<string, RateLimiterOptions>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Named bulkheads (concurrency limiters) keyed by registry name.</summary>
    public IDictionary<string, BulkheadOptions> Bulkheads { get; set; } = new Dictionary<string, BulkheadOptions>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Named time limiters (timeouts) keyed by registry name.</summary>
    public IDictionary<string, TimeLimiterOptions> TimeLimiters { get; set; } = new Dictionary<string, TimeLimiterOptions>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Configures a Polly circuit breaker. Defaults to Resilience4j-style 50% / 30s window.</summary>
public sealed class CircuitBreakerOptions
{
    /// <summary>Failure ratio (0.0 – 1.0) within <see cref="SamplingDuration"/> that opens the breaker.</summary>
    public double FailureRateThreshold { get; set; } = 0.5;

    /// <summary>Sliding window over which the failure ratio is computed.</summary>
    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Minimum number of requests in the window before the breaker can open.</summary>
    public int MinimumThroughput { get; set; } = 10;

    /// <summary>How long the breaker stays open before transitioning to half-open.</summary>
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>Configures a Polly retry strategy.</summary>
public sealed class RetryOptions
{
    /// <summary>Maximum retry attempts (the original call counts as attempt 0).</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Base delay between attempts before backoff is applied.</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Backoff curve: <c>Constant</c>, <c>Linear</c>, or <c>Exponential</c>.</summary>
    public string BackoffType { get; set; } = "Exponential";

    /// <summary>If <c>true</c>, randomizes the delay slightly to prevent thundering herds.</summary>
    public bool UseJitter { get; set; } = true;
}

/// <summary>Configures a sliding-window rate limiter.</summary>
public sealed class RateLimiterOptions
{
    /// <summary>How many calls may be admitted within <see cref="Window"/> before throttling kicks in.</summary>
    public int PermitLimit { get; set; } = 100;

    /// <summary>Length of the sliding window.</summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Maximum waiters queued when the limit is reached. <c>0</c> = reject immediately.</summary>
    public int QueueLimit { get; set; } = 0;
}

/// <summary>Configures a bulkhead (concurrency limiter).</summary>
public sealed class BulkheadOptions
{
    /// <summary>Maximum concurrent in-flight calls.</summary>
    public int MaxConcurrency { get; set; } = 25;

    /// <summary>Maximum waiters queued when concurrency is saturated. <c>0</c> = reject immediately.</summary>
    public int MaxQueue { get; set; } = 50;
}

/// <summary>Configures a time limiter (per-call timeout).</summary>
public sealed class TimeLimiterOptions
{
    /// <summary>Wall-clock duration before the call is cancelled.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
