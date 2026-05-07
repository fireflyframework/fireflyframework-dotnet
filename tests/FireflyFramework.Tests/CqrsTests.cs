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

using FireflyFramework.Cqrs.Buses;
using FireflyFramework.Cqrs.Commands;
using FireflyFramework.Cqrs.DependencyInjection;
using FireflyFramework.Cqrs.Validation;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ExecutionContext = FireflyFramework.Cqrs.Context.ExecutionContext;

namespace FireflyFramework.Tests;

public sealed record GreetCommand(string Name) : ICommand<string>
{
    public Task<ValidationResult> ValidateAsync(CancellationToken ct = default) =>
        Task.FromResult(string.IsNullOrWhiteSpace(Name)
            ? ValidationResult.Failed("Name", "Name is required")
            : ValidationResult.Successful());
}

public sealed class GreetHandler : ICommandHandler<GreetCommand, string>
{
    public Task<string> HandleAsync(GreetCommand command, ExecutionContext context, CancellationToken ct = default) =>
        Task.FromResult($"Hello, {command.Name}!");
}

public class CqrsTests
{
    [Fact]
    public async Task CommandBus_dispatches_to_registered_handler()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<DefaultCommandBus>>(_ => NullLogger<DefaultCommandBus>.Instance);
        services.AddFireflyCqrs(typeof(GreetCommand).Assembly);
        var sp = services.BuildServiceProvider();

        var bus = sp.GetRequiredService<ICommandBus>();
        var result = await bus.SendAsync(new GreetCommand("World"));
        result.Should().Be("Hello, World!");
    }

    [Fact]
    public async Task CommandBus_throws_when_validation_fails()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<DefaultCommandBus>>(_ => NullLogger<DefaultCommandBus>.Instance);
        services.AddFireflyCqrs(typeof(GreetCommand).Assembly);
        var sp = services.BuildServiceProvider();

        var bus = sp.GetRequiredService<ICommandBus>();
        await FluentActions.Invoking(() => bus.SendAsync(new GreetCommand(string.Empty)))
            .Should().ThrowAsync<CqrsValidationException>();
    }
}
