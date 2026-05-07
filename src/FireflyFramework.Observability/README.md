# FireflyFramework.Observability

OpenTelemetry .NET wiring for traces, metrics, and logs, plus Serilog
enrichment and Kubernetes-friendly health primitives. Mirrors
`org.fireflyframework:firefly-otel-spring-boot-starter`.

## Wiring

```csharp
using FireflyFramework.Observability.DependencyInjection;

builder.Services.AddFireflyObservability(
    builder.Configuration,
    serviceName:    "orders-service",
    serviceVersion: "1.0.0");
```

`AddFireflyObservability`:

- Binds the `Firefly:Observability` configuration section.
- Configures the OpenTelemetry resource with `service.name`,
  `service.version`, and `framework=fireflyframework-dotnet`.
- Registers `Meter` instrumentation under the `firefly.*` namespace,
  plus `AddRuntimeInstrumentation`, with OTLP export.
- Registers `ActivitySource` instrumentation under `firefly.*` with the
  configured sampling ratio and W3C propagation, exported via OTLP.

Application code instruments itself with the standard
`System.Diagnostics.Metrics` and `System.Diagnostics.ActivitySource`
APIs:

```csharp
using System.Diagnostics;
using System.Diagnostics.Metrics;

private static readonly ActivitySource ActivitySource = new("firefly.orders");
private static readonly Meter         Meter          = new("firefly.orders");
private static readonly Histogram<double> OrderDuration = Meter.CreateHistogram<double>(
    "firefly.orders.placement.duration",
    unit: "ms",
    description: "Time taken to place an order");

using var activity = ActivitySource.StartActivity("PlaceOrder");
var sw = Stopwatch.StartNew();
try
{
    // ... place the order
}
finally
{
    OrderDuration.Record(sw.Elapsed.TotalMilliseconds, new("status", "ok"));
}
```

## Public surface

| Type / namespace                                | Purpose                                                                |
|-------------------------------------------------|------------------------------------------------------------------------|
| `FireflyObservabilityOptions`                   | Root options bound to `Firefly:Observability`                          |
| `MetricsOptions`                                | Enabled / Prefix / Exporter (`Prometheus | Otlp | Both`) / OtlpEndpoint |
| `TracingOptions`                                | SamplingProbability / Propagation / Bridge / BaggageFields             |
| `HealthOptions`                                 | Enabled / KubernetesProbes                                             |
| `LoggingOptions`                                | Enabled / StructuredFormat                                             |
| `Metrics.MetricNaming` / `Metrics.MetricTags`   | Conventional metric naming and tag names                               |
| `Metrics.FireflyMetricsSupport`                 | Helpers for histograms / counters scoped to "firefly.*"                |
| `Tracing.FireflyTracingSupport`                 | Helpers for `ActivitySource` creation                                  |
| `Logging.MdcConstants`                          | Standard log-context keys (`correlationId`, `tenantId`, etc.)          |
| `Health.FireflyHealthCheck`                     | Aggregate health check that surfaces every `IHealthCheck`              |

## Configuration

```json
{
  "Firefly": {
    "Observability": {
      "Metrics": {
        "Enabled":      true,
        "Prefix":       "firefly",
        "Exporter":     "Both",
        "OtlpEndpoint": "http://otel-collector:4317"
      },
      "Tracing": {
        "Enabled":             true,
        "SamplingProbability": 0.1,
        "Propagation":         "W3C",
        "BaggageFields":       [ "tenant-id", "correlation-id" ],
        "OtlpEndpoint":        "http://otel-collector:4317"
      },
      "Health":  { "Enabled": true, "KubernetesProbes": true },
      "Logging": { "Enabled": true, "StructuredFormat": true }
    }
  }
}
```

## Dependencies

| Reference                                  | Used for                          |
|--------------------------------------------|-----------------------------------|
| `FireflyFramework.Kernel`                  | Calendar version                  |
| `OpenTelemetry`                            | Tracing + metrics SDK             |
| `OpenTelemetry.Extensions.Hosting`         | Hosted-service integration         |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | OTLP exporter                  |
| `OpenTelemetry.Instrumentation.AspNetCore` | Inbound HTTP instrumentation      |
| `OpenTelemetry.Instrumentation.Http`       | Outbound `HttpClient` instrumentation |
| `OpenTelemetry.Instrumentation.Runtime`    | GC / thread-pool / process metrics |
| `Serilog`                                  | Structured logging                |

## Java mapping

| .NET                                   | Java                                                     |
|----------------------------------------|----------------------------------------------------------|
| `AddFireflyObservability`              | `FireflyOtelAutoConfiguration`                           |
| `FireflyObservabilityOptions`          | `FireflyObservabilityProperties`                         |
| `Metrics.MetricNaming`                 | `MetricNaming`                                           |
| `Metrics.MetricTags`                   | `MetricTags`                                             |
| `Tracing.FireflyTracingSupport`        | `FireflyTracingSupport`                                  |
