// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Actuator.DependencyInjection;
using FireflyFramework.Actuator.Hosting;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace FireflyFramework.Tests;

public sealed class ActuatorRouteTests
{
    [Fact]
    public async Task Actuator_router_serves_info_and_index_endpoints()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(builder => builder
                .UseTestServer()
                .ConfigureServices(s =>
                {
                    var config = new ConfigurationBuilder()
                        .AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["Firefly:Actuator:BasePath"] = "/actuator",
                        }).Build();
                    s.AddSingleton<IConfiguration>(config);
                    s.AddRouting();
                    s.AddFireflyActuator(config);
                    // Limit exposure explicitly so the 404 path is testable.
                    s.PostConfigure<FireflyFramework.Actuator.Configuration.FireflyActuatorOptions>(opt =>
                    {
                        opt.ExposeEndpoints.Clear();
                        opt.ExposeEndpoints.Add("info");
                        opt.ExposeEndpoints.Add("metrics");
                    });
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(e => e.MapFireflyActuator());
                }))
            .StartAsync();

        var client = host.GetTestClient();

        var index = await client.GetAsync("/actuator");
        index.IsSuccessStatusCode.Should().BeTrue();
        var indexBody = await index.Content.ReadAsStringAsync();
        indexBody.Should().Contain("info").And.Contain("metrics");

        var info = await client.GetAsync("/actuator/info");
        info.IsSuccessStatusCode.Should().BeTrue();
        var infoBody = await info.Content.ReadAsStringAsync();
        infoBody.Should().Contain("app").And.Contain("runtime");

        // Endpoint not in ExposeEndpoints list returns 404.
        var beans = await client.GetAsync("/actuator/beans");
        beans.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }
}
