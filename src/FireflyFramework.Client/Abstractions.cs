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
