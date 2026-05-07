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
