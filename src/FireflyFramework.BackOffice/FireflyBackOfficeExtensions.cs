using FireflyFramework.Starter.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FireflyFramework.BackOffice;

public static class FireflyBackOfficeExtensions
{
    /// <summary>
    /// Registers everything from <see cref="FireflyApplicationExtensions.AddFireflyApplication"/>
    /// plus the back-office context resolver. Replace
    /// <see cref="HeaderBackofficeContextResolver"/> with a service-specific subclass to plug
    /// in your security center.
    /// </summary>
    public static IServiceCollection AddFireflyBackOffice(
        this IServiceCollection services,
        IConfiguration config,
        string serviceName,
        string serviceVersion = "1.0.0",
        params System.Reflection.Assembly[] cqrsAssemblies)
    {
        services.AddFireflyApplication(config, serviceName, serviceVersion, cqrsAssemblies);
        services.TryAddScoped<IBackofficeContextResolver, HeaderBackofficeContextResolver>();
        return services;
    }
}
