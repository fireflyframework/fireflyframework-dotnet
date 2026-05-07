using System.Diagnostics.Metrics;

namespace FireflyFramework.Client.Metrics;

/// <summary>
/// Thin metrics emitter for service clients. Mirrors Java <c>ServiceClientMetrics</c>. All
/// counters and histograms publish under the <c>firefly.client.*</c> namespace and pick up
/// any <see cref="MeterListener"/> the host has wired (OpenTelemetry, Prometheus, console).
/// </summary>
public sealed class ServiceClientMetrics : IDisposable
{
    public const string MeterName = "FireflyFramework.Client";

    private readonly Meter _meter;
    private readonly Counter<long> _requests;
    private readonly Counter<long> _errors;
    private readonly Histogram<double> _durationMs;
    private readonly Counter<long> _retries;
    private readonly Counter<long> _circuitBreakerOpens;

    public ServiceClientMetrics()
    {
        _meter = new Meter(MeterName);
        _requests = _meter.CreateCounter<long>("firefly.client.requests", description: "Total outbound client requests");
        _errors = _meter.CreateCounter<long>("firefly.client.errors", description: "Outbound client requests that returned an error");
        _durationMs = _meter.CreateHistogram<double>("firefly.client.duration", unit: "ms", description: "Outbound client request latency");
        _retries = _meter.CreateCounter<long>("firefly.client.retries", description: "Retried outbound requests");
        _circuitBreakerOpens = _meter.CreateCounter<long>("firefly.client.circuit_breaker.opens", description: "Circuit-breaker open transitions");
    }

    public void RecordRequest(string serviceName, string method, int statusCode, TimeSpan duration)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("service", serviceName),
            new("method", method),
            new("status", statusCode),
        };
        _requests.Add(1, tags);
        _durationMs.Record(duration.TotalMilliseconds, tags);
        if (statusCode >= 400) _errors.Add(1, tags);
    }

    public void RecordRetry(string serviceName, string method, int attempt)
    {
        _retries.Add(1,
            new KeyValuePair<string, object?>("service", serviceName),
            new KeyValuePair<string, object?>("method", method),
            new KeyValuePair<string, object?>("attempt", attempt));
    }

    public void RecordCircuitBreakerOpen(string serviceName)
    {
        _circuitBreakerOpens.Add(1, new KeyValuePair<string, object?>("service", serviceName));
    }

    public void Dispose() => _meter.Dispose();
}
