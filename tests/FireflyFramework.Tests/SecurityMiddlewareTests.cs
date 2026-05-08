// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Security.Claims;
using FireflyFramework.Security.Core;
using FireflyFramework.Security.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace FireflyFramework.Tests;

public sealed class SecurityMiddlewareTests
{
    [Fact]
    public async Task Middleware_binds_HttpContext_User_to_SecurityContextHolder()
    {
        ISecurityContextHolder? holder = null;
        SecurityContext captured = SecurityContext.Anonymous;

        using var host = await new HostBuilder()
            .ConfigureWebHost(builder => builder
                .UseTestServer()
                .ConfigureServices(s => s.AddSingleton<ISecurityContextHolder, AsyncLocalSecurityContextHolder>())
                .Configure(app =>
                {
                    app.Use(async (ctx, next) =>
                    {
                        // simulate authenticated user
                        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                        {
                            new Claim("sub", "user-1"),
                            new Claim(ClaimTypes.Role, "ADMIN"),
                        }, authenticationType: "test"));
                        await next();
                    });
                    app.UseFireflySecurityContext();
                    app.Run(ctx =>
                    {
                        holder = ctx.RequestServices.GetRequiredService<ISecurityContextHolder>();
                        captured = holder.Current;
                        return Task.CompletedTask;
                    });
                }))
            .StartAsync();

        var client = host.GetTestClient();
        var response = await client.GetAsync("/");

        response.IsSuccessStatusCode.Should().BeTrue();
        captured.IsAuthenticated.Should().BeTrue();
        captured.SubjectId.Should().Be("user-1");
        captured.HasRole("ADMIN").Should().BeTrue();

        // Outside the request, the holder pops back to Anonymous.
        holder!.Current.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task Middleware_carries_X_Tenant_Id_header_through_to_SecurityContext()
    {
        SecurityContext captured = SecurityContext.Anonymous;

        using var host = await new HostBuilder()
            .ConfigureWebHost(builder => builder
                .UseTestServer()
                .ConfigureServices(s => s.AddSingleton<ISecurityContextHolder, AsyncLocalSecurityContextHolder>())
                .Configure(app =>
                {
                    app.Use(async (ctx, next) =>
                    {
                        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "u") }, "test"));
                        await next();
                    });
                    app.UseFireflySecurityContext();
                    app.Run(ctx =>
                    {
                        captured = ctx.RequestServices.GetRequiredService<ISecurityContextHolder>().Current;
                        return Task.CompletedTask;
                    });
                }))
            .StartAsync();

        var client = host.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Tenant-Id", "tenant-99");
        await client.SendAsync(request);

        captured.TenantId.Should().Be("tenant-99");
    }
}
