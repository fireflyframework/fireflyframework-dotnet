using FireflyFramework.EventSourcing.Store;
using FireflyFramework.Starter.Core;
using FireflyFramework.Web.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FireflyFramework.Starter.Domain;

/// <summary>
/// Domain-tier starter: includes everything in the core starter plus an
/// in-memory event store. Replace the event store with the EF Core implementation by
/// calling <c>AddEfCoreEventStore&lt;TDbContext&gt;()</c> from <c>FireflyFramework.EventSourcing.Store.EfCore</c>
/// if you need persistence.
/// </summary>
public static class FireflyDomainExtensions
{
    public static IServiceCollection AddFireflyDomain(
        this IServiceCollection services,
        IConfiguration config,
        string serviceName,
        string serviceVersion = "1.0.0",
        params System.Reflection.Assembly[] cqrsAssemblies)
    {
        FireflyBanner.Print(typeof(FireflyDomainExtensions).Assembly, serviceName, serviceVersion);
        services.AddFireflyCore(config, serviceName, serviceVersion, cqrsAssemblies);
        services.TryAddSingleton<IEventStore, InMemoryEventStore>();
        return services;
    }
}
