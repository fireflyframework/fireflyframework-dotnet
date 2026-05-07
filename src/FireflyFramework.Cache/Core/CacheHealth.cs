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

namespace FireflyFramework.Cache.Core;

public enum CacheHealthStatus
{
    Healthy,
    Degraded,
    Unhealthy,
    NotConfigured,
}

/// <summary>Cache health snapshot. Mirrors Java <c>CacheHealth</c>.</summary>
public sealed record CacheHealth(
    CacheHealthStatus Status,
    CacheType Type,
    string Name,
    bool Available,
    bool Configured,
    DateTimeOffset CheckedAt,
    TimeSpan? ResponseTime,
    DateTimeOffset? LastSuccessfulOperation,
    int ConsecutiveFailures,
    Dictionary<string, object?> Details,
    string? ErrorMessage,
    Exception? Cause)
{
    public bool IsHealthy => Status == CacheHealthStatus.Healthy;

    public static CacheHealth Healthy(CacheType type, string name) => new(
        CacheHealthStatus.Healthy, type, name, true, true,
        DateTimeOffset.UtcNow, null, DateTimeOffset.UtcNow, 0,
        new Dictionary<string, object?>(), null, null);

    public static CacheHealth Unhealthy(CacheType type, string name, string? error, Exception? cause = null) => new(
        CacheHealthStatus.Unhealthy, type, name, false, true,
        DateTimeOffset.UtcNow, null, null, 0,
        new Dictionary<string, object?>(), error, cause);

    public static CacheHealth NotConfigured(CacheType type, string name) => new(
        CacheHealthStatus.NotConfigured, type, name, false, false,
        DateTimeOffset.UtcNow, null, null, 0,
        new Dictionary<string, object?>(), null, null);
}
