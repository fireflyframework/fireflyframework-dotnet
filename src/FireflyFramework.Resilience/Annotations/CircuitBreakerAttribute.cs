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

namespace FireflyFramework.Resilience.Annotations;

/// <summary>
/// Marks a method to be wrapped with a Polly circuit breaker. Mirrors Java
/// Resilience4j <c>@CircuitBreaker</c> and pyfly <c>@circuit_breaker</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class CircuitBreakerAttribute : Attribute
{
    public CircuitBreakerAttribute(string name) { Name = name; }
    public string Name { get; }
    public string? Fallback { get; set; }
}

/// <summary>Marks a method to be wrapped with a Polly retry strategy.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class RetryAttribute : Attribute
{
    public RetryAttribute(string name) { Name = name; }
    public string Name { get; }
    public string? Fallback { get; set; }
}

/// <summary>Marks a method to be wrapped with a Polly rate limiter.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class RateLimiterAttribute : Attribute
{
    public RateLimiterAttribute(string name) { Name = name; }
    public string Name { get; }
}

/// <summary>Marks a method to be wrapped with a bulkhead (concurrency limiter).</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class BulkheadAttribute : Attribute
{
    public BulkheadAttribute(string name) { Name = name; }
    public string Name { get; }
}

/// <summary>Marks a method to be wrapped with a time limiter (timeout).</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class TimeLimiterAttribute : Attribute
{
    public TimeLimiterAttribute(string name) { Name = name; }
    public string Name { get; }
}

/// <summary>Marks a method that should fall back when the primary fails.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class FallbackAttribute : Attribute
{
    public FallbackAttribute(string fallbackMethod) { FallbackMethod = fallbackMethod; }
    public string FallbackMethod { get; }
}
