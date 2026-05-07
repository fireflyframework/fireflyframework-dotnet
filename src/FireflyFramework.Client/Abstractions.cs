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

namespace FireflyFramework.Client;

/// <summary>Resilience config for the client builder. Mirrors Java <c>CircuitBreakerConfig</c>.</summary>
public sealed class ClientResilienceOptions
{
    public bool CircuitBreakerEnabled { get; set; } = true;
    public double FailureRateThreshold { get; set; } = 0.5;
    public TimeSpan SlowCallDurationThreshold { get; set; } = TimeSpan.FromSeconds(2);
    public int SlidingWindowSize { get; set; } = 20;
    public int MinimumThroughput { get; set; } = 10;
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(30);
    public int RetryAttempts { get; set; } = 3;
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(200);
    public bool RetryJitter { get; set; } = true;
}

public enum AuthScheme { None, ApiKey, Bearer, OAuth2ClientCredentials, BasicAuth, Mtls }

public sealed class ClientAuthOptions
{
    public AuthScheme Scheme { get; set; } = AuthScheme.None;
    public string? ApiKey { get; set; }
    public string? ApiKeyHeader { get; set; } = "X-Api-Key";
    public string? BearerToken { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? OAuth2TokenUrl { get; set; }
    public string? OAuth2ClientId { get; set; }
    public string? OAuth2ClientSecret { get; set; }
    public string? OAuth2Scope { get; set; }
    public string? ClientCertPath { get; set; }
    public string? ClientCertPassword { get; set; }
}
