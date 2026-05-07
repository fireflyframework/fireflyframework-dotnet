// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Actuator.Core;
using FireflyFramework.Aop.Core;
using FireflyFramework.I18n.Core;
using FireflyFramework.Resilience.Core;
using FireflyFramework.Scheduling.Core;
using FireflyFramework.Security.Core;
using FireflyFramework.Session.Core;
using FireflyFramework.Starter.Application;
using FireflyFramework.WebSocket.Core;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Xunit;

namespace FireflyFramework.Tests;

/// <summary>
/// Integration test: AddFireflyApplication must register every module the
/// application-tier starter advertises. If a future change drops a module
/// from the bundle, this test breaks the build before consumers do.
/// </summary>
public sealed class StarterApplicationWiringTests
{
    [Fact]
    public void AddFireflyApplication_registers_all_advertised_modules()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Firefly:Security:Jwt:Issuer"] = "test",
                ["Firefly:Security:Jwt:Audience"] = "test",
                ["Firefly:Security:Jwt:Secret"] = "this-is-a-very-long-secret-key-for-test-only-not-prod",
                ["Firefly:Session:Provider"] = "Memory",
                ["Firefly:Actuator:BasePath"] = "/actuator",
                ["Firefly:I18n:DefaultLocale"] = "en",
            }).Build();

        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();

        // Standalone ServiceCollection lacks the IHostEnvironment that the InfoEndpoint
        // depends on; substitute one so the actuator side of the starter resolves.
        var env = Substitute.For<IHostEnvironment>();
        env.ApplicationName.Returns("wiring-test");
        env.EnvironmentName.Returns("Test");
        services.AddSingleton(env);

        services.AddFireflyApplication(config, "wiring-test", "1.0.0");

        var sp = services.BuildServiceProvider();

        // Resilience
        sp.GetService<IResilienceRegistry>().Should().NotBeNull();
        // Security
        sp.GetService<ISecurityContextHolder>().Should().NotBeNull();
        // Actuator
        sp.GetServices<IActuatorEndpoint>().Should().NotBeEmpty();
        // Scheduling
        sp.GetService<ITaskScheduler>().Should().NotBeNull();
        sp.GetService<ITaskExecutor>().Should().NotBeNull();
        // Session
        sp.GetService<ISessionStore>().Should().NotBeNull();
        // I18n
        sp.GetService<IMessageSource>().Should().NotBeNull();
        sp.GetService<ILocaleResolver>().Should().NotBeNull();
        // AOP
        sp.GetService<IAspectRegistry>().Should().NotBeNull();
        // WebSocket
        sp.GetService<IWebSocketSessionRegistry>().Should().NotBeNull();
    }
}
