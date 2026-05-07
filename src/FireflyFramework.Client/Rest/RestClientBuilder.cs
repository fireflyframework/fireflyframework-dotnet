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

using System.Net.Http.Headers;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace FireflyFramework.Client.Rest;

/// <summary>
/// Fluent builder for resilient HTTP clients. Mirrors Java <c>RestClientBuilder</c>.
/// </summary>
public sealed class RestClientBuilder
{
    private string? _baseUrl;
    private TimeSpan _timeout = TimeSpan.FromSeconds(30);
    private readonly Dictionary<string, string> _defaultHeaders = new();
    private ClientResilienceOptions _resilience = new();
    private ClientAuthOptions _auth = new();

    public static RestClientBuilder Create() => new();

    public RestClientBuilder WithBaseUrl(string url) { _baseUrl = url; return this; }
    public RestClientBuilder WithTimeout(TimeSpan t) { _timeout = t; return this; }
    public RestClientBuilder WithDefaultHeader(string name, string value) { _defaultHeaders[name] = value; return this; }
    public RestClientBuilder WithResilience(Action<ClientResilienceOptions> cfg) { cfg(_resilience); return this; }
    public RestClientBuilder WithAuth(Action<ClientAuthOptions> cfg) { cfg(_auth); return this; }

    public HttpClient Build()
    {
        var inner = new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(10) };
        var resilience = BuildResiliencePipeline();
        var handler = new ResilientHandler(inner, resilience);
        var client = new HttpClient(handler) { Timeout = _timeout };
        if (_baseUrl is not null) client.BaseAddress = new Uri(_baseUrl);
        foreach (var (k, v) in _defaultHeaders)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(k, v);
        }

        ApplyAuth(client);
        return client;
    }

    private void ApplyAuth(HttpClient client)
    {
        switch (_auth.Scheme)
        {
            case AuthScheme.ApiKey when _auth.ApiKey is not null && _auth.ApiKeyHeader is not null:
                client.DefaultRequestHeaders.TryAddWithoutValidation(_auth.ApiKeyHeader, _auth.ApiKey);
                break;
            case AuthScheme.Bearer when _auth.BearerToken is not null:
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _auth.BearerToken);
                break;
            case AuthScheme.BasicAuth when _auth.Username is not null:
                var raw = $"{_auth.Username}:{_auth.Password}";
                var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encoded);
                break;
        }
    }

    private ResiliencePipeline<HttpResponseMessage> BuildResiliencePipeline()
    {
        var b = new ResiliencePipelineBuilder<HttpResponseMessage>();
        if (_resilience.CircuitBreakerEnabled)
        {
            b.AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = _resilience.FailureRateThreshold,
                MinimumThroughput = _resilience.MinimumThroughput,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = _resilience.BreakDuration,
            });
        }

        b.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
        {
            MaxRetryAttempts = _resilience.RetryAttempts,
            Delay = _resilience.RetryBaseDelay,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = _resilience.RetryJitter,
        });

        b.AddTimeout(_timeout);
        return b.Build();
    }
}

internal sealed class ResilientHandler : DelegatingHandler
{
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;

    public ResilientHandler(HttpMessageHandler inner, ResiliencePipeline<HttpResponseMessage> pipeline) : base(inner)
    {
        _pipeline = pipeline;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
        _pipeline.ExecuteAsync(async token => await base.SendAsync(request, token).ConfigureAwait(false), ct).AsTask();
}
