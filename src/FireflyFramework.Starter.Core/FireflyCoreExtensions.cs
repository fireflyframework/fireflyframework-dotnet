using FireflyFramework.Cache.DependencyInjection;
using FireflyFramework.Cqrs.DependencyInjection;
using FireflyFramework.Eda.DependencyInjection;
using FireflyFramework.Observability.DependencyInjection;
using FireflyFramework.Web.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FireflyFramework.Starter.Core;

/// <summary>
/// One-call registration of the Firefly infrastructure tier — equivalent to importing
/// <c>fireflyframework-starter-core</c> on the Java side.
/// </summary>
public static class FireflyCoreExtensions
{
    public static IServiceCollection AddFireflyCore(
        this IServiceCollection services,
        IConfiguration config,
        string serviceName,
        string serviceVersion = "1.0.0",
        params System.Reflection.Assembly[] cqrsAssemblies)
    {
        services.AddFireflyWeb(config);
        services.AddFireflyObservability(config, serviceName, serviceVersion);
        services.AddFireflyCache(config);
        services.AddFireflyEda(config);
        services.AddFireflyCqrs(cqrsAssemblies);
        return services;
    }
}
