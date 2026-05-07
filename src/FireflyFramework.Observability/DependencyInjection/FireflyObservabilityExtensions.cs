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
