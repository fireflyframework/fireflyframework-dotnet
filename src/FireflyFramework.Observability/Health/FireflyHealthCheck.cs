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

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FireflyFramework.Observability.Health;

/// <summary>
/// Convenience base for component health checks that want to surface latency and
/// connection-pool metrics. Mirrors Java <c>FireflyHealthIndicator</c>.
/// </summary>
public abstract class FireflyHealthCheck : IHealthCheck
{
    protected FireflyHealthCheck(string componentName) => ComponentName = componentName;

    public string ComponentName { get; }

    public abstract Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default);

    protected static Dictionary<string, object> Detail(string key, object value, Dictionary<string, object>? existing = null)
    {
        existing ??= new Dictionary<string, object>();
        existing[key] = value;
        return existing;
    }

    protected static HealthCheckResult ErrorRate(double rate, double threshold, Dictionary<string, object>? existing = null)
    {
        var details = Detail("errorRate", rate, existing);
        details["errorRateThreshold"] = threshold;
        return rate > threshold
            ? HealthCheckResult.Unhealthy("error rate above threshold", null, details)
            : HealthCheckResult.Healthy("ok", details);
    }

    protected static HealthCheckResult Latency(double p99Ms, double thresholdMs, Dictionary<string, object>? existing = null)
    {
        var details = Detail("p99Ms", p99Ms, existing);
        details["p99ThresholdMs"] = thresholdMs;
        return p99Ms > thresholdMs
            ? HealthCheckResult.Unhealthy("latency above threshold", null, details)
            : HealthCheckResult.Healthy("ok", details);
    }

    protected static HealthCheckResult ConnectionPool(int active, int idle, int max, Dictionary<string, object>? existing = null)
    {
        var details = Detail("active", active, existing);
        details["idle"] = idle;
        details["max"] = max;
        return active >= max
            ? HealthCheckResult.Unhealthy("connection pool exhausted", null, details)
            : HealthCheckResult.Healthy("ok", details);
    }
}
