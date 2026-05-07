using FireflyFramework.Plugins.Api;
using FireflyFramework.Plugins.Core;
using FireflyFramework.Starter.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FireflyFramework.Starter.Application;

/// <summary>
/// Application-tier starter: includes everything in the core starter plus the plugin
/// extension registry and manager. IDP / orchestration / rule-engine wiring remains
/// application-specific because each service picks one adapter — register them in your
/// composition root.
/// </summary>
public static class FireflyApplicationExtensions
{
    public static IServiceCollection AddFireflyApplication(
        this IServiceCollection services,
        IConfiguration config,
        string serviceName,
        string serviceVersion = "1.0.0",
        params System.Reflection.Assembly[] cqrsAssemblies)
    {
        services.AddFireflyCore(config, serviceName, serviceVersion, cqrsAssemblies);
        services.TryAddSingleton<IExtensionRegistry, DefaultExtensionRegistry>();
        services.TryAddSingleton<IPluginManager, DefaultPluginManager>();
        return services;
    }
}
