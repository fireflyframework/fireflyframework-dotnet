# FireflyFramework.Observability

## Overview

`FireflyFramework.Observability` is the **OpenTelemetry-first
observability tier** of the Firefly framework. It wires the three
canonical signals — distributed traces, metrics, and structured logs —
under a single `AddFireflyObservability(...)` call, then layers
Firefly-specific conventions on top: a `firefly.*` resource attribute,
a metric-naming guard, the standard low-cardinality tag set, and the
log-context keys that bind log lines back to their owning trace.

The module mirrors `org.fireflyframework:firefly-otel-spring-boot-starter`
in scope and behaviour. The Java starter wires Micrometer + Brave +
Logback MDC; this .NET port wires `System.Diagnostics.Metrics` +
`System.Diagnostics.ActivitySource` + Serilog. Both export over OTLP
to whatever collector the operator runs (typically OpenTelemetry
Collector → Tempo / Prometheus / Loki).

## Why a separate module?

ASP.NET 10 ships the OpenTelemetry primitives in the framework, but
turning them on for a service still requires:

- One configuration block per signal (traces, metrics, logs).
- A resource that includes `service.name`, `service.version`, and a
  framework discriminator so back-ends can group every Firefly
  service.
- Sampler, propagator, and exporter selection that's consistent across
  every service in the platform.
- Naming conventions so dashboards built for one service work for all.

This module bundles all of that into one extension method plus a
typed options class. You opt into observability at the call site
(`AddFireflyObservability(...)`); you opt out by leaving it out.
There's no implicit "auto-instrumentation" — every signal is
explicitly wired, which keeps surprises low when something goes wrong
in production.

## Mental model

```
                    ┌────────────────────────────────────────┐
                    │  AddFireflyObservability(name, ver)    │
                    └─────────┬──────────────────────────────┘
                              │
            ┌─────────────────┼─────────────────┐
            │                 │                 │
       ┌────▼─────┐      ┌────▼────┐      ┌─────▼────┐
       │  Traces  │      │ Metrics │      │   Logs   │
       │ (Activity│      │ (Meter) │      │(Serilog) │
       │  Source) │      │         │      │          │
       └────┬─────┘      └────┬────┘      └─────┬────┘
            │                 │                 │
            ▼                 ▼                 ▼
        ┌──────────────────────────────────────────┐
        │   OTLP exporter (HTTP or gRPC, 4317/4318) │
        └──────────────────┬───────────────────────┘
                           ▼
                ┌────────────────────────┐
                │  OpenTelemetry         │
                │  Collector (sidecar    │
                │  or daemonset)         │
                └────────┬───────────────┘
                         │
        ┌────────────────┼────────────────┐
        │                │                │
   ┌────▼────┐      ┌────▼────┐     ┌─────▼────┐
   │  Tempo  │      │Prometheus│     │   Loki   │
   │ (traces)│      │ (metrics)│     │  (logs)  │
   └─────────┘      └─────────┘     └──────────┘
```

The collector is the integration seam. Anything that speaks OTLP
plugs in — Jaeger, Tempo, AWS X-Ray, Datadog, Honeycomb, New Relic.

## Quick start

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
- Registers `ActivitySource` instrumentation under `firefly.*` with
  the configured sampling ratio and W3C propagation, exported via
  OTLP.

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
    activity?.SetTag(MetricTags.Status, MetricTags.Success);
}
catch (Exception ex)
{
    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    activity?.SetTag(MetricTags.Status, MetricTags.Failure);
    throw;
}
finally
{
    OrderDuration.Record(sw.Elapsed.TotalMilliseconds, new("status", "ok"));
}
```

## Public surface

### Configuration types

| Type                            | Section path                   | Purpose                                  |
|---------------------------------|--------------------------------|------------------------------------------|
| `FireflyObservabilityOptions`   | `Firefly:Observability`        | Root options                             |
| `MetricsOptions`                | `Firefly:Observability:Metrics`| Enabled / Prefix / Exporter / OtlpEndpoint |
| `TracingOptions`                | `Firefly:Observability:Tracing`| SamplingProbability / Propagation / Bridge / BaggageFields |
| `HealthOptions`                 | `Firefly:Observability:Health` | Enabled / KubernetesProbes               |
| `LoggingOptions`                | `Firefly:Observability:Logging`| Enabled / StructuredFormat               |
| `MetricsExporter`               | enum                           | `Prometheus`, `Otlp`, `Both`             |
| `TracingBridge`                 | enum                           | `OpenTelemetry`, `Brave`                 |
| `PropagationType`               | enum                           | `W3C`, `B3`                              |

### Naming and tag conventions

| Type                              | Purpose                                                        |
|-----------------------------------|----------------------------------------------------------------|
| `Metrics.MetricNaming.Prefix`     | Validates a module name and returns `firefly.<module>` prefix  |
| `Metrics.MetricNaming.Name`       | Concatenates prefix + metric name                              |
| `Metrics.MetricTags`              | Standard low-cardinality tag names (`status`, `error.type`, …) |
| `Logging.MdcConstants`            | Standard log-context keys (`correlationId`, `tenantId`, …)     |
| `Tracing.FireflyTracingSupport`   | Helpers for `ActivitySource` creation                          |
| `Metrics.FireflyMetricsSupport`   | Helpers for histograms / counters scoped to "firefly.*"        |

The `MetricNaming.Prefix("orders")` call validates that the module
name is lowercase + alphanumeric + underscore, then returns
`"firefly.orders"`. The whole framework uses this guard so misnamed
metrics fail at startup, not at production-time when a dashboard
silently empties.

#### Standard low-cardinality tag set

| Tag name             | Used by                                | Allowed values (examples)                |
|----------------------|----------------------------------------|------------------------------------------|
| `status`             | every metric                           | `success`, `failure`, `timeout`, `rejected`, `cancelled` |
| `error.type`         | metrics where status = failure         | `validation`, `timeout`, `unauthorized`, `unavailable` |
| `operation`          | client / server                        | RPC method or HTTP route template        |
| `command.type`       | CQRS                                   | `PlaceOrderCommand`                      |
| `query.type`         | CQRS                                   | `GetOrderByIdQuery`                      |
| `event.type`         | EDA                                    | `order.placed`                           |
| `workflow.id`        | orchestration                          | `OrderApproval`                          |
| `step.id`            | orchestration                          | `02-await-approval`                      |
| `provider`           | adapters                               | `keycloak`, `aws_cognito`, `azure_ad`    |
| `channel`            | notifications                          | `email`, `sms`, `push`                   |
| `aggregate.type`     | event-sourcing                         | `Order`                                  |

Tag *values* must be low cardinality — never user ids, request ids, or
opaque hashes. Trace tags are the place for those.

### Health checks

`FireflyHealthCheck` is an abstract base class for component health
checks that exposes three convenience helpers operators care about:

| Helper                                          | Returns                                                                   |
|-------------------------------------------------|---------------------------------------------------------------------------|
| `Latency(p99Ms, thresholdMs, …)`                | Healthy/Unhealthy comparison with the threshold + p99 in details          |
| `ErrorRate(rate, threshold, …)`                 | Healthy/Unhealthy comparison with the threshold + observed rate in details |
| `ConnectionPool(active, idle, max, …)`          | Healthy until pool is full; details carry pool counters                    |

Compose them in your own `IHealthCheck` implementation:

```csharp
public sealed class OrderRepositoryHealthCheck(IRepoMetrics m) : FireflyHealthCheck("order-repo")
{
    public override Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext ctx, CancellationToken ct = default)
    {
        var detail = Detail("backend", "postgres");
        if (m.LastErrorRate > 0.05) return Task.FromResult(ErrorRate(m.LastErrorRate, 0.05, detail));
        return Task.FromResult(Latency(m.P99Ms, 250.0, detail));
    }
}
```

When `HealthOptions.KubernetesProbes` is enabled, ASP.NET maps the
results to the standard `/health/live` (liveness) and `/health/ready`
(readiness) endpoints expected by Kubernetes probes.

### Log-context keys (MdcConstants)

The Java line uses Logback's MDC (Mapped Diagnostic Context) for
per-request log enrichment; .NET uses Serilog's `LogContext` (or any
`IExternalScopeProvider`). Either way, the *names* of the standard
keys are the same:

| Constant                   | Value                  | Purpose                                          |
|----------------------------|------------------------|--------------------------------------------------|
| `MdcConstants.TraceId`     | `traceId`              | OpenTelemetry trace id (32-char hex)             |
| `MdcConstants.SpanId`      | `spanId`               | OpenTelemetry span id (16-char hex)              |
| `MdcConstants.TransactionId` | `transactionId`      | Business transaction id (orchestration / saga)   |
| `MdcConstants.TransactionIdHeader` | `X-Transaction-Id` | Header carrying the transaction id           |
| `MdcConstants.UserId`      | `userId`               | Authenticated user (or `anonymous`)              |
| `MdcConstants.CorrelationId` | `correlationId`      | Cross-service correlation                        |
| `MdcConstants.RequestId`   | `requestId`            | Per-HTTP-request id                              |
| `MdcConstants.ServiceName` | `service.name`         | Aligns with the OpenTelemetry resource attribute |
| `MdcConstants.AggregateType` | `aggregate.type`     | Event-sourcing aggregate type                    |
| `MdcConstants.AggregateId` | `aggregate.id`         | Event-sourcing aggregate id                      |

The Web layer's correlation-id middleware automatically pushes
`correlationId`, `requestId`, and `userId` into the log context so
every log line emitted during an HTTP request carries them.

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

| Property                                | Default            | Effect                                                         |
|-----------------------------------------|--------------------|----------------------------------------------------------------|
| `Metrics.Enabled`                       | `true`             | Master switch for metric collection                            |
| `Metrics.Prefix`                        | `firefly`          | Prefix imposed by `MetricNaming` (overriding requires re-validation) |
| `Metrics.Exporter`                      | `Both`             | OTLP, Prometheus scrape endpoint, or both                      |
| `Metrics.OtlpEndpoint`                  | (env-derived)      | Override the OTLP target — defaults to `OTEL_EXPORTER_OTLP_ENDPOINT` |
| `Tracing.Enabled`                       | `true`             | Master switch for tracing                                      |
| `Tracing.SamplingProbability`           | `1.0`              | TraceIdRatioBased sampler — 1.0 = always sample, 0.1 = 10%     |
| `Tracing.Propagation`                   | `W3C`              | `W3C` (traceparent) or `B3` (b3 single header)                 |
| `Tracing.Bridge`                        | `OpenTelemetry`    | OpenTelemetry direct or Zipkin/Brave format                    |
| `Tracing.BaggageFields`                 | tenant-id, correlation-id | OpenTelemetry baggage keys propagated cross-service       |
| `Health.KubernetesProbes`               | `true`             | Map `/health/live` + `/health/ready`                           |
| `Logging.StructuredFormat`              | `true`             | Emit Serilog `CompactJson`; otherwise human-readable           |

## Common patterns

### Instrumenting a request handler

```csharp
private static readonly ActivitySource Source = new("firefly.orders");
private static readonly Meter          Meter  = new("firefly.orders");
private static readonly Counter<long>  Placed = Meter.CreateCounter<long>(
    "firefly.orders.placed", "{order}", "Total orders placed");

public async Task<OrderId> HandleAsync(PlaceOrderCommand cmd, CancellationToken ct)
{
    using var activity = Source.StartActivity("PlaceOrder");
    activity?.SetTag(MetricTags.CommandType, nameof(PlaceOrderCommand));

    try
    {
        var id = await _orders.PlaceAsync(cmd, ct);
        activity?.SetTag(MetricTags.Status, MetricTags.Success);
        Placed.Add(1, new(MetricTags.Status, MetricTags.Success));
        return id;
    }
    catch (Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.SetTag(MetricTags.Status, MetricTags.Failure);
        activity?.SetTag(MetricTags.ErrorType, ex.GetType().Name);
        Placed.Add(1, new(MetricTags.Status, MetricTags.Failure));
        throw;
    }
}
```

### Custom Serilog enricher

```csharp
public sealed class TenantEnricher(IHttpContextAccessor accessor) : ILogEventEnricher
{
    public void Enrich(LogEvent evt, ILogEventPropertyFactory factory)
    {
        var tenant = accessor.HttpContext?.Items["tenantId"] as string;
        if (!string.IsNullOrEmpty(tenant))
        {
            evt.AddPropertyIfAbsent(factory.CreateProperty("tenantId", tenant));
        }
    }
}

builder.Host.UseSerilog((ctx, _, logger) => logger
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.With(new TenantEnricher(httpAccessor))
    .Enrich.WithProperty(MdcConstants.ServiceName, "orders-service"));
```

### Sampling a high-volume span sparingly

For "noisy" hot-path spans you'd otherwise drown traces in, mark the
activity as `Recorded.Drop` unless an upstream sampler decision was
taken:

```csharp
using var activity = Source.StartActivity("Cache.Get",
    ActivityKind.Internal,
    parentContext: default,
    links: null,
    startTime: default);

if (Random.Shared.NextDouble() > 0.01)
{
    activity?.IsAllDataRequested = false;   // drop unless explicitly enabled
}
```

## Pitfalls and gotchas

- **Tag values must be low cardinality.** A tag whose values are
  unbounded (request id, user id, opaque hash) will explode your
  metric back-end. Put unbounded values on trace spans where they
  don't aggregate.
- **`firefly.*` is the meter and source pattern.** Anything outside
  `firefly.*` is not picked up by the framework's exporters. If you
  reuse a third-party `Meter`/`ActivitySource`, register it
  explicitly:

  ```csharp
  builder.Services.ConfigureOpenTelemetryMeterProvider(
      m => m.AddMeter("MyService.Custom"));
  ```

- **Sampling is per-trace, not per-span.** A `SamplingProbability` of
  `0.1` keeps 10% of *traces* (every span in that trace), not 10% of
  spans. For "always sample errors but downsample successes" you need
  a `ParentBased` sampler with a `RatioBasedSampler` head.
- **`StructuredFormat=false` reverts to plain-text logs.** That breaks
  the trace ↔ log join in Loki / Datadog. Only turn it off for local
  dev when you need readable console output.
- **`KubernetesProbes=true` does not register the routes.** It tells
  the health-check service to *expose* readiness/liveness; you still
  need `app.MapHealthChecks("/health/live", …)` in your pipeline. The
  starter packs do this for you.
- **OTLP endpoint resolution.** When unset in `Firefly:Observability`,
  the SDK falls back to `OTEL_EXPORTER_OTLP_ENDPOINT` (and the more
  specific `OTEL_EXPORTER_OTLP_TRACES_ENDPOINT` /
  `_METRICS_ENDPOINT` / `_LOGS_ENDPOINT`). When configured here, this
  setting wins. Be deliberate about which layer owns the choice.

## Internals (for the curious)

- `MetricNaming.Prefix` uses a compile-time regex source generator
  (`[GeneratedRegex]`) so the validation is allocation-free and
  trim-friendly.
- The OTLP exporter defaults to gRPC on port 4317; the operator can
  switch to HTTP/protobuf on 4318 by setting
  `OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf`.
- Runtime instrumentation collects GC pause times, thread-pool
  saturation, exception count, and process CPU/memory — these arrive
  as `dotnet.gc.*`, `dotnet.thread_pool.*`, etc., and are picked up
  by the `dotnet` Grafana dashboard out of the box.
- The Java line uses Brave for tracing; .NET uses OpenTelemetry's
  native `ActivitySource` API. Spans created via either bridge are
  semantically interoperable so long as both ends speak the same
  propagator (W3C is the default on both).

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
| `Logging.MdcConstants`                 | `MdcConstants` (Logback MDC keys)                        |
| `Health.FireflyHealthCheck`            | `FireflyHealthIndicator`                                 |
