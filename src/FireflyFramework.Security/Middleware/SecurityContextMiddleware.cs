// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Security.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace FireflyFramework.Security.Middleware;

/// <summary>
/// Binds the current ASP.NET Core <c>HttpContext.User</c> to a Firefly
/// <see cref="SecurityContext"/> exposed via <see cref="ISecurityContextHolder"/>.
/// </summary>
public sealed class SecurityContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ISecurityContextHolder _holder;

    public SecurityContextMiddleware(RequestDelegate next, ISecurityContextHolder holder)
    {
        _next = next;
        _holder = holder;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var tenantId = context.Request.Headers["X-Tenant-Id"].ToString();
        var ctx = SecurityContext.FromClaimsPrincipal(context.User, string.IsNullOrEmpty(tenantId) ? null : tenantId);
        using (_holder.Push(ctx)) await _next(context).ConfigureAwait(false);
    }
}

public static class SecurityContextMiddlewareExtensions
{
    public static IApplicationBuilder UseFireflySecurityContext(this IApplicationBuilder app) =>
        app.UseMiddleware<SecurityContextMiddleware>();
}
