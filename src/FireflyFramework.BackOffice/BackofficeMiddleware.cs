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
