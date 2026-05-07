// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Session.Adapters;
using FireflyFramework.Session.Configuration;
using FireflyFramework.Session.Core;
using FireflyFramework.Session.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace FireflyFramework.Tests;

public sealed class SessionMiddlewareTests
{
    [Fact]
    public async Task Middleware_issues_session_cookie_and_round_trips_attributes()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(builder => builder
                .UseTestServer()
                .ConfigureServices(s =>
                {
                    s.AddSingleton<ISessionStore, InMemorySessionStore>();
                    s.AddSingleton(Options.Create(new FireflySessionOptions
                    {
                        CookieName = "FIREFLY_SESSION",
                        SecureCookie = false,
                        MaxInactiveInterval = TimeSpan.FromMinutes(10),
                    }));
                })
                .Configure(app =>
                {
                    app.UseFireflySession();
                    app.Run(ctx =>
                    {
                        var session = ctx.GetFireflySession()!;
                        if (ctx.Request.Path == "/set")
                        {
                            session.Set("user", "ana");
                            return ctx.Response.WriteAsync("set");
                        }
                        if (ctx.Request.Path == "/get")
                        {
                            session.TryGet<string>("user", out var user);
                            return ctx.Response.WriteAsync(user ?? "<none>");
                        }
                        return Task.CompletedTask;
                    });
                }))
            .StartAsync();

        var client = host.GetTestClient();

        // First request — no cookie sent. Server should issue one.
        var first = await client.GetAsync("/set");
        first.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        var cookie = cookies!.First();
        cookie.Should().Contain("FIREFLY_SESSION=");

        // Extract cookie value and replay it.
        var cookieValue = cookie.Split(';')[0]; // e.g. FIREFLY_SESSION=abc123
        var second = new HttpRequestMessage(HttpMethod.Get, "/get");
        second.Headers.Add("Cookie", cookieValue);
        var response = await client.SendAsync(second);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("ana");
    }
}
