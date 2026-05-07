using FireflyFramework.Starter.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FireflyFramework.Starter.Data;

/// <summary>
/// Data-tier starter: includes everything in the core starter. The application is
/// expected to call <c>services.AddDbContext&lt;TDb&gt;(...)</c> with its own
/// <see cref="Microsoft.EntityFrameworkCore.DbContext"/>; the Firefly filtering
/// utilities (<c>FireflyFramework.Data.Filters</c>) and pagination types are made
/// available transitively.
/// </summary>
public static class FireflyDataExtensions
{
    public static IServiceCollection AddFireflyData(
        this IServiceCollection services,
        IConfiguration config,
        string serviceName,
        string serviceVersion = "1.0.0",
        params System.Reflection.Assembly[] cqrsAssemblies) =>
        services.AddFireflyCore(config, serviceName, serviceVersion, cqrsAssemblies);
}
