// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.Resilience.Configuration;

/// <summary>Configuration root for the resilience module. Mirrors Java <c>Resilience4jProperties</c>.</summary>
public sealed class FireflyResilienceOptions
{
    public const string SectionName = "Firefly:Resilience";

    public IDictionary<string, CircuitBreakerOptions> CircuitBreakers { get; set; } = new Dictionary<string, CircuitBreakerOptions>(StringComparer.OrdinalIgnoreCase);
    public IDictionary<string, RetryOptions> Retries { get; set; } = new Dictionary<string, RetryOptions>(StringComparer.OrdinalIgnoreCase);
    public IDictionary<string, RateLimiterOptions> RateLimiters { get; set; } = new Dictionary<string, RateLimiterOptions>(StringComparer.OrdinalIgnoreCase);
    public IDictionary<string, BulkheadOptions> Bulkheads { get; set; } = new Dictionary<string, BulkheadOptions>(StringComparer.OrdinalIgnoreCase);
    public IDictionary<string, TimeLimiterOptions> TimeLimiters { get; set; } = new Dictionary<string, TimeLimiterOptions>(StringComparer.OrdinalIgnoreCase);
}

public sealed class CircuitBreakerOptions
{
    public double FailureRateThreshold { get; set; } = 0.5;
    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(30);
    public int MinimumThroughput { get; set; } = 10;
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(30);
}

public sealed class RetryOptions
{
    public int MaxAttempts { get; set; } = 3;
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(500);
    public string BackoffType { get; set; } = "Exponential"; // Constant | Linear | Exponential
    public bool UseJitter { get; set; } = true;
}

public sealed class RateLimiterOptions
{
    public int PermitLimit { get; set; } = 100;
    public TimeSpan Window { get; set; } = TimeSpan.FromSeconds(1);
    public int QueueLimit { get; set; } = 0;
}

public sealed class BulkheadOptions
{
    public int MaxConcurrency { get; set; } = 25;
    public int MaxQueue { get; set; } = 50;
}

public sealed class TimeLimiterOptions
{
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
