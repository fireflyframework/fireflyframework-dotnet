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

using System.Text.Json.Serialization;

namespace FireflyFramework.Web.Errors.Models;

/// <summary>Circuit-breaker context for 503 responses. Mirrors Java <c>CircuitBreakerInfo</c>.</summary>
public sealed class CircuitBreakerInfo
{
    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("failureRate")]
    public double FailureRate { get; set; }

    [JsonPropertyName("failureRateThreshold")]
    public double FailureRateThreshold { get; set; }

    [JsonPropertyName("failureCount")]
    public long FailureCount { get; set; }

    [JsonPropertyName("nextAttemptTime")]
    public DateTimeOffset? NextAttemptTime { get; set; }

    [JsonPropertyName("fallbackSuggestion")]
    public string? FallbackSuggestion { get; set; }
}
