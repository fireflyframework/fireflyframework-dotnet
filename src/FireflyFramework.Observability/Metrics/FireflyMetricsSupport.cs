using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace FireflyFramework.Observability.Metrics;

/// <summary>
/// Base class for module-specific metrics services. Mirrors Java <c>FireflyMetricsSupport</c>:
/// concrete subclasses pass a module name, then call <see cref="Counter"/>,
/// <see cref="Histogram"/>, <see cref="Timed{T}"/> etc. The names are auto-prefixed with
/// "firefly.{module}".
/// </summary>
public abstract class FireflyMetricsSupport
{
    private readonly Meter _meter;
    private readonly ConcurrentDictionary<string, Counter<long>> _counters = new();
    private readonly ConcurrentDictionary<string, Histogram<double>> _histograms = new();

    protected FireflyMetricsSupport(IMeterFactory meterFactory, string module)
    {
        Module = module;
        ModulePrefix = MetricNaming.Prefix(module);
        _meter = meterFactory.Create(ModulePrefix);
    }

    public string Module { get; }

    public string ModulePrefix { get; }

    public Counter<long> Counter(string name) => _counters.GetOrAdd(name, n => _meter.CreateCounter<long>(MetricNaming.Name(ModulePrefix, n)));

    public Histogram<double> Histogram(string name) => _histograms.GetOrAdd(name, n => _meter.CreateHistogram<double>(MetricNaming.Name(ModulePrefix, n)));

    public ObservableGauge<long> Gauge(string name, Func<long> supplier, params KeyValuePair<string, object?>[] tags) =>
        _meter.CreateObservableGauge(MetricNaming.Name(ModulePrefix, name), () => new Measurement<long>(supplier(), tags));

    public async Task<T> Timed<T>(string metric, Func<Task<T>> work, params KeyValuePair<string, object?>[] tags)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await work().ConfigureAwait(false);
            Histogram(metric).Record(sw.Elapsed.TotalMilliseconds, [.. tags, new(MetricTags.Status, MetricTags.Success)]);
            return result;
        }
        catch (Exception ex)
        {
            Histogram(metric).Record(sw.Elapsed.TotalMilliseconds, [.. tags, new(MetricTags.Status, MetricTags.Failure), new(MetricTags.ErrorType, ex.GetType().Name)]);
            throw;
        }
    }

    public void RecordSuccess(string metric, params KeyValuePair<string, object?>[] tags) =>
        Counter(metric).Add(1, [.. tags, new(MetricTags.Status, MetricTags.Success)]);

    public void RecordFailure(string metric, Exception ex, params KeyValuePair<string, object?>[] tags) =>
        Counter(metric).Add(1, [.. tags, new(MetricTags.Status, MetricTags.Failure), new(MetricTags.ErrorType, ex.GetType().Name)]);
}
