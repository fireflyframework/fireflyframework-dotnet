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

using System.Diagnostics.Metrics;
using FireflyFramework.Client.Metrics;
using Xunit;

namespace FireflyFramework.Tests;

/// <summary>
/// Tests for <see cref="ServiceClientMetrics"/>. Use a <see cref="MeterListener"/> to capture
/// the emitted measurements and assert the counter / histogram names + tags + values are
/// what downstream OpenTelemetry / Prometheus bridges will pick up.
/// </summary>
public sealed class ServiceClientMetricsTests
{
    [Fact]
    public void RecordRequest_EmitsRequestCounter_DurationHistogram_AndErrorCounterFor4xx()
    {
        var observed = new List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == ServiceClientMetrics.MeterName) l.EnableMeasurementEvents(instrument);
            },
        };
        listener.SetMeasurementEventCallback<long>((i, v, t, _) => observed.Add((i.Name, v, t.ToArray())));
        listener.SetMeasurementEventCallback<double>((i, v, t, _) => observed.Add((i.Name, v, t.ToArray())));
        listener.Start();

        using var metrics = new ServiceClientMetrics();
        metrics.RecordRequest("users", "GET", 404, TimeSpan.FromMilliseconds(123));

        var requestEvent = observed.SingleOrDefault(e => e.Name == "firefly.client.requests");
        Assert.Equal(1, requestEvent.Value);
        Assert.Contains(requestEvent.Tags, t => t.Key == "service" && (string?)t.Value == "users");
        Assert.Contains(requestEvent.Tags, t => t.Key == "method" && (string?)t.Value == "GET");
        Assert.Contains(requestEvent.Tags, t => t.Key == "status" && (int?)t.Value == 404);

        var durationEvent = observed.SingleOrDefault(e => e.Name == "firefly.client.duration");
        Assert.Equal(123d, durationEvent.Value);

        var errorEvent = observed.SingleOrDefault(e => e.Name == "firefly.client.errors");
        Assert.Equal(1, errorEvent.Value);
    }

    [Fact]
    public void RecordRetry_AndCircuitBreakerOpen_EmitTaggedCounters()
    {
        var observed = new List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == ServiceClientMetrics.MeterName) l.EnableMeasurementEvents(instrument);
            },
        };
        listener.SetMeasurementEventCallback<long>((i, v, t, _) => observed.Add((i.Name, v, t.ToArray())));
        listener.Start();

        using var metrics = new ServiceClientMetrics();
        metrics.RecordRetry("users", "POST", 2);
        metrics.RecordCircuitBreakerOpen("orders");

        var retry = observed.Single(e => e.Name == "firefly.client.retries");
        Assert.Equal(1, retry.Value);
        Assert.Contains(retry.Tags, t => t.Key == "attempt" && (int?)t.Value == 2);

        var open = observed.Single(e => e.Name == "firefly.client.circuit_breaker.opens");
        Assert.Contains(open.Tags, t => t.Key == "service" && (string?)t.Value == "orders");
    }
}
