namespace FireflyFramework.Observability.Configuration;

/// <summary>Root configuration. Mirrors Java <c>FireflyObservabilityProperties</c>.</summary>
public sealed class FireflyObservabilityOptions
{
    public const string SectionName = "Firefly:Observability";

    public MetricsOptions Metrics { get; set; } = new();
    public TracingOptions Tracing { get; set; } = new();
    public HealthOptions Health { get; set; } = new();
    public LoggingOptions Logging { get; set; } = new();
}

public sealed class MetricsOptions
{
    public bool Enabled { get; set; } = true;
    public string Prefix { get; set; } = "firefly";
    public MetricsExporter Exporter { get; set; } = MetricsExporter.Both;
    public string? OtlpEndpoint { get; set; }
}

public sealed class TracingOptions
{
    public bool Enabled { get; set; } = true;
    public TracingBridge Bridge { get; set; } = TracingBridge.OpenTelemetry;
    public double SamplingProbability { get; set; } = 1.0;
    public PropagationType Propagation { get; set; } = PropagationType.W3C;
    public List<string> BaggageFields { get; set; } = new() { "tenant-id", "correlation-id" };
    public string? OtlpEndpoint { get; set; }
}

public sealed class HealthOptions
{
    public bool Enabled { get; set; } = true;
    public bool KubernetesProbes { get; set; } = true;
}

public sealed class LoggingOptions
{
    public bool Enabled { get; set; } = true;
    public bool StructuredFormat { get; set; } = true;
}

public enum MetricsExporter { Prometheus, Otlp, Both }
public enum TracingBridge { OpenTelemetry, Brave }
public enum PropagationType { W3C, B3 }
