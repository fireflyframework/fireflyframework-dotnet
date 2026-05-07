using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using FireflyFramework.Callbacks.Interfaces;
using FireflyFramework.Callbacks.Models;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace FireflyFramework.Callbacks.Core;

public interface ICallbackDispatcher
{
    Task<CallbackExecutionDto> DispatchAsync(CallbackConfigurationEntity config, string eventType, string payload, CancellationToken ct = default);
}

public sealed class CallbackDispatcher : ICallbackDispatcher
{
    private readonly HttpClient _http;
    private readonly ILogger<CallbackDispatcher> _log;

    public CallbackDispatcher(HttpClient http, ILogger<CallbackDispatcher> log)
    {
        _http = http;
        _log = log;
    }

    public async Task<CallbackExecutionDto> DispatchAsync(
        CallbackConfigurationEntity config, string eventType, string payload, CancellationToken ct = default)
    {
        var pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = config.MaxRetries,
                Delay = TimeSpan.FromMilliseconds(config.RetryDelayMs),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
            })
            .AddTimeout(TimeSpan.FromMilliseconds(config.TimeoutMs))
            .Build();

        using var req = new HttpRequestMessage(MapMethod(config.HttpMethod), config.Url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };

        if (config.SignatureEnabled && config.Secret is not null)
        {
            var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(config.Secret));
            var sig = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
            req.Headers.TryAddWithoutValidation(config.SignatureHeader ?? "X-Signature", sig);
        }

        var started = DateTimeOffset.UtcNow;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var resp = await pipeline.ExecuteAsync(async _ => await _http.SendAsync(req, ct).ConfigureAwait(false), ct).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return new CallbackExecutionDto(
                Guid.NewGuid(), config.Id, eventType, Guid.NewGuid().ToString(),
                resp.IsSuccessStatusCode ? CallbackExecutionStatus.Success : CallbackExecutionStatus.FailedRetrying,
                payload, null, (int)resp.StatusCode, body,
                1, config.MaxRetries, sw.ElapsedMilliseconds, null, started, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Callback dispatch failed for {Url}", config.Url);
            return new CallbackExecutionDto(
                Guid.NewGuid(), config.Id, eventType, Guid.NewGuid().ToString(),
                CallbackExecutionStatus.FailedPermanent,
                payload, null, null, null,
                1, config.MaxRetries, sw.ElapsedMilliseconds, ex.Message, started, DateTimeOffset.UtcNow);
        }
    }

    private static HttpMethod MapMethod(CallbackHttpMethod m) => m switch
    {
        CallbackHttpMethod.Post => HttpMethod.Post,
        CallbackHttpMethod.Put => HttpMethod.Put,
        CallbackHttpMethod.Patch => HttpMethod.Patch,
        _ => HttpMethod.Post,
    };
}
