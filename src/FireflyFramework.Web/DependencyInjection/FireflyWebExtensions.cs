using FireflyFramework.Web.Cors;
using FireflyFramework.Web.Errors.Converters;
using FireflyFramework.Web.Idempotency;
using FireflyFramework.Web.Logging;
using FireflyFramework.Web.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FireflyFramework.Web.DependencyInjection;

/// <summary>
/// Service / middleware registration for the Firefly web stack. Replaces Spring Boot
/// auto-configuration (<c>ExceptionHandlerAutoConfiguration</c>, <c>CorsAutoConfiguration</c>,
/// <c>IdempotencyAutoConfiguration</c>, <c>PiiMaskingAutoConfiguration</c>).
/// </summary>
public static class FireflyWebExtensions
{
    public static IServiceCollection AddFireflyWeb(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<GlobalExceptionHandlerOptions>()
            .Bind(config.GetSection(GlobalExceptionHandlerOptions.SectionName));
        services.AddOptions<PiiMaskingOptions>()
            .Bind(config.GetSection(PiiMaskingOptions.SectionName));
        services.AddOptions<IdempotencyOptions>()
            .Bind(config.GetSection(IdempotencyOptions.SectionName));
        services.AddOptions<FireflyCorsOptions>()
            .Bind(config.GetSection(FireflyCorsOptions.SectionName));

        services.TryAddSingleton<PiiMaskingService>();
        services.TryAddSingleton<ExceptionConverterRegistry>();

        // Default converters — equivalent to Java's auto-discovered ExceptionConverter beans.
        services.AddSingleton<IExceptionConverter, OperationCanceledExceptionConverter>();
        services.AddSingleton<IExceptionConverter, TimeoutExceptionConverter>();
        services.AddSingleton<IExceptionConverter, JsonExceptionConverter>();
        services.AddSingleton<IExceptionConverter, HttpRequestExceptionConverter>();
        services.AddSingleton<IExceptionConverter, UnauthorizedAccessExceptionConverter>();
        services.AddSingleton<IExceptionConverter, ArgumentExceptionConverter>();
        services.AddSingleton<IExceptionConverter, NotImplementedExceptionConverter>();
        services.AddSingleton<IExceptionConverter, InvalidOperationExceptionConverter>();

        // Idempotency requires a distributed cache; default to memory if none registered yet.
        services.AddDistributedMemoryCache();

        return services;
    }

    public static IApplicationBuilder UseFireflyWeb(this IApplicationBuilder app)
    {
        app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
        app.UseMiddleware<IdempotencyMiddleware>();
        return app;
    }
}
