// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Shell.Annotations;
using FireflyFramework.Shell.Core;
using FireflyFramework.Shell.DependencyInjection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FireflyFramework.Tests;

public sealed class ShellTests
{
    [Fact]
    public void ApplicationArguments_parses_long_options_and_positionals()
    {
        var args = new ApplicationArguments(new[] { "doit", "--name=alice", "--verbose", "ext" });
        args.NonOptionArgs.Should().BeEquivalentTo(new[] { "doit", "ext" });
        args.OptionArgs["name"].Should().Be("alice");
        args.ContainsOption("verbose").Should().BeTrue();
    }

    [Fact]
    public async Task DefaultShellRunner_dispatches_to_decorated_method()
    {
        var component = new GreetingComponent();
        var services = new ServiceCollection()
            .AddSingleton<IFireflyShellComponent>(component)
            .AddLogging()
            .BuildServiceProvider();

        var runner = new DefaultShellRunner(services, NullLogger<DefaultShellRunner>.Instance);
        var code = await runner.RunOnceAsync(new[] { "greet", "Ana" }, CancellationToken.None);

        code.Should().Be(0);
        component.Greeted.Should().Be("Ana");
    }

    [Fact]
    public async Task Unknown_verb_returns_nonzero_code()
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var runner = new DefaultShellRunner(services, NullLogger<DefaultShellRunner>.Instance);
        var code = await runner.RunOnceAsync(new[] { "no-such-command" }, CancellationToken.None);
        code.Should().Be(1);
    }

    [ShellComponent]
    public sealed class GreetingComponent : IFireflyShellComponent
    {
        public string? Greeted { get; private set; }

        [ShellMethod(Description = "Say hi")]
        public Task Greet([ShellArgument] string who)
        {
            Greeted = who;
            return Task.CompletedTask;
        }
    }
}
