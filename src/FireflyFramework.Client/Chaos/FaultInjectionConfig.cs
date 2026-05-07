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

namespace FireflyFramework.Client.Chaos;

/// <summary>
/// Chaos-engineering fault-injection configuration. Mirrors Java
/// <c>FaultInjectionConfig</c>. Every category of fault is independently toggled and gated
/// by a probability between 0.0 and 1.0:
///
/// <list type="bullet">
/// <item><b>Latency injection</b> — uniform-random delay between
///   <see cref="MinLatency"/> and <see cref="MaxLatency"/>.</item>
/// <item><b>Error injection</b> — throws a configurable exception type before the request
///   reaches the network.</item>
/// <item><b>Timeout injection</b> — waits past a configured threshold to force a downstream
///   timeout.</item>
/// <item><b>Service filtering</b> — opt in / out per service via
///   <see cref="IncludedServices"/> + <see cref="ExcludedServices"/>.</item>
/// </list>
///
/// <para>Defaults disable everything so accidental wire-up in production is a no-op. Tests
/// flip flags + probabilities to 1.0 to assert the interceptor's behaviour deterministically.</para>
/// </summary>
public sealed class FaultInjectionConfig
{
    public bool Enabled { get; set; }

    /// <summary>If non-empty, only these services are subject to injection.</summary>
    public HashSet<string> IncludedServices { get; set; } = new();
    /// <summary>Always exempt; takes precedence over <see cref="IncludedServices"/>.</summary>
    public HashSet<string> ExcludedServices { get; set; } = new();

    // ── Latency ────────────────────────────────────────────
    public bool LatencyInjectionEnabled { get; set; }
    public double LatencyProbability { get; set; }
    public TimeSpan MinLatency { get; set; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan MaxLatency { get; set; } = TimeSpan.FromSeconds(2);

    // ── Error ──────────────────────────────────────────────
    public bool ErrorInjectionEnabled { get; set; }
    public double ErrorProbability { get; set; }
    /// <summary>Factory that returns the exception to throw on injection.</summary>
    public Func<Exception> ErrorFactory { get; set; } = () => new ChaosException("Chaos error injection");

    // ── Timeout ────────────────────────────────────────────
    public bool TimeoutInjectionEnabled { get; set; }
    public double TimeoutProbability { get; set; }
    public TimeSpan TimeoutDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>True iff the service is subject to chaos injection per <see cref="IncludedServices"/> / <see cref="ExcludedServices"/>.</summary>
    public bool AppliesTo(string serviceName)
    {
        if (!Enabled) return false;
        if (ExcludedServices.Contains(serviceName)) return false;
        if (IncludedServices.Count > 0 && !IncludedServices.Contains(serviceName)) return false;
        return true;
    }
}

/// <summary>Marker exception thrown by chaos-engineering injection. Lets callers distinguish injected faults from real ones.</summary>
public sealed class ChaosException : Exception
{
    public ChaosException(string message) : base(message) { }
}
