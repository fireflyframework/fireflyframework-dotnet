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

using FireflyFramework.Observability.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace FireflyFramework.Observability.DependencyInjection;

public static class FireflyObservabilityExtensions
{
    public static IServiceCollection AddFireflyObservability(
        this IServiceCollection services,
        IConfiguration config,
        string serviceName,
        string serviceVersion = "1.0.0")
    {
        services.AddOptions<FireflyObservabilityOptions>().Bind(config.GetSection(FireflyObservabilityOptions.SectionName));

        var resource = ResourceBuilder.CreateDefault()
            .AddService(serviceName, serviceVersion: serviceVersion)
            .AddAttributes(new[]
            {
                new KeyValuePair<string, object>("framework", "fireflyframework-dotnet"),
            });

        services.AddOpenTelemetry()
            .ConfigureResource(b => b.AddService(serviceName, serviceVersion: serviceVersion))
            .WithMetrics(b =>
            {
                b.AddMeter("firefly.*");
                b.AddRuntimeInstrumentation();
                b.AddOtlpExporter();
            })
            .WithTracing(b =>
            {
                b.AddSource("firefly.*");
                b.SetSampler(new TraceIdRatioBasedSampler(1.0));
                b.AddOtlpExporter();
            });

        return services;
    }
}
