using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FireflyFramework.BackOffice;

/// <summary>
/// Resolves a <see cref="BackofficeContext"/> on every back-office request and stores it
/// in <see cref="HttpContext.Items"/> under <see cref="ContextKey"/> for downstream
/// handlers. Mirrors Java's <c>BackofficeContextWebFilter</c>.
/// </summary>
public sealed class BackofficeMiddleware
{
    public const string ContextKey = "Firefly.BackofficeContext";
    private readonly RequestDelegate _next;
    private readonly ILogger<BackofficeMiddleware> _log;

    public BackofficeMiddleware(RequestDelegate next, ILogger<BackofficeMiddleware> log)
    {
        _next = next;
        _log = log;
    }

    public async Task InvokeAsync(HttpContext httpContext, IBackofficeContextResolver resolver)
    {
        try
        {
            var ctx = await resolver.ResolveAsync(httpContext, httpContext.RequestAborted).ConfigureAwait(false);
            httpContext.Items[ContextKey] = ctx;
        }
        catch (InvalidOperationException ex)
        {
            _log.LogWarning(ex, "Back-office context resolution failed: {Reason}", ex.Message);
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsync(ex.Message).ConfigureAwait(false);
            return;
        }

        await _next(httpContext).ConfigureAwait(false);
    }
}

public static class BackofficeApplicationBuilderExtensions
{
    public static IApplicationBuilder UseFireflyBackoffice(this IApplicationBuilder app) =>
        app.UseMiddleware<BackofficeMiddleware>();

    public static BackofficeContext? GetBackofficeContext(this HttpContext httpContext) =>
        httpContext.Items.TryGetValue(BackofficeMiddleware.ContextKey, out var v)
            ? v as BackofficeContext
            : null;
}
