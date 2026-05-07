// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Actuator.Endpoints;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Xunit;

namespace FireflyFramework.Tests;

public sealed class ActuatorTests
{
    [Fact]
    public async Task InfoEndpoint_emits_app_and_runtime_info()
    {
        var env = Substitute.For<IHostEnvironment>();
        env.ApplicationName.Returns("test-app");
        env.EnvironmentName.Returns("Development");

        var endpoint = new InfoEndpoint(env);
        var payload = await endpoint.InvokeAsync(new Dictionary<string, string?>(), CancellationToken.None);

        payload.Should().NotBeNull();
        payload!.GetType().GetProperty("app")!.GetValue(payload).Should().NotBeNull();
    }

    [Fact]
    public async Task EnvEndpoint_masks_secret_keys()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:Name"] = "demo",
                ["Db:Password"] = "supersecret",
                ["Auth:JwtSecret"] = "topsecret",
            }).Build();

        var endpoint = new EnvEndpoint(config);
        var payload = await endpoint.InvokeAsync(new Dictionary<string, string?>(), CancellationToken.None);
        var json = System.Text.Json.JsonSerializer.Serialize(payload);

        json.Should().Contain("\"App:Name\"");
        json.Should().NotContain("supersecret");
        json.Should().NotContain("topsecret");
        json.Should().Contain("***");
    }

    [Fact]
    public async Task BeansEndpoint_emits_registrations()
    {
        var registrations = new List<BeansEndpoint.BeanRegistration>
        {
            new("Foo.IBar", "Foo.Bar", "Singleton", false),
            new("Foo.IBaz", "Foo.Baz", "Scoped", false),
        };
        var endpoint = new BeansEndpoint(registrations);
        var payload = await endpoint.InvokeAsync(new Dictionary<string, string?>(), CancellationToken.None);
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        json.Should().Contain("Foo.IBar");
        json.Should().Contain("Singleton");
    }

    [Fact]
    public async Task MetricsEndpoint_emits_process_and_gc_data()
    {
        var endpoint = new MetricsEndpoint();
        var payload = await endpoint.InvokeAsync(new Dictionary<string, string?>(), CancellationToken.None);
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        json.Should().Contain("process").And.Contain("gc").And.Contain("uptime");
    }
}
