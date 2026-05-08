// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Cli.Templates;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace FireflyFramework.Tests;

public sealed class CliScaffoldTests
{
    [Fact]
    public void HandlerScaffold_command_output_parses_as_valid_csharp()
    {
        var src = HandlerScaffold.Render("CreateOrder", "command");
        var tree = CSharpSyntaxTree.ParseText(src);
        tree.GetDiagnostics().Should().BeEmpty($"scaffold should parse:\n{src}");

        src.Should().Contain("ICommand<Unit>");
        src.Should().Contain("ICommandHandler<CreateOrder, Unit>");
    }

    [Fact]
    public void HandlerScaffold_query_output_parses_as_valid_csharp()
    {
        var src = HandlerScaffold.Render("GetOrder", "query");
        var tree = CSharpSyntaxTree.ParseText(src);
        tree.GetDiagnostics().Should().BeEmpty($"scaffold should parse:\n{src}");

        src.Should().Contain("IQuery<GetOrderResult>");
        src.Should().Contain("IQueryHandler<GetOrder, GetOrderResult>");
    }

    [Fact]
    public void SagaScaffold_emits_compensable_methods_for_each_step()
    {
        var src = SagaScaffold.Render("PaymentSaga", steps: 3);
        var tree = CSharpSyntaxTree.ParseText(src);
        tree.GetDiagnostics().Should().BeEmpty($"scaffold should parse:\n{src}");

        src.Should().Contain("Step1Async").And.Contain("CompensateStep1Async")
           .And.Contain("Step2Async").And.Contain("CompensateStep2Async")
           .And.Contain("Step3Async").And.Contain("CompensateStep3Async");
    }

    [Fact]
    public void ServiceScaffold_writes_real_solution_file_with_five_modules()
    {
        var temp = Path.Combine(Path.GetTempPath(), "firefly-scaffold-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            ServiceScaffold.Render(temp, new TemplateContext("orders-service", "OrdersService", "core"));

            var sln = Path.Combine(temp, "OrdersService.sln");
            File.Exists(sln).Should().BeTrue();
            var slnContent = File.ReadAllText(sln);
            slnContent.Should().Contain("Microsoft Visual Studio Solution File, Format Version 12.00");
            slnContent.Should().Contain("OrdersService.Interfaces");
            slnContent.Should().Contain("OrdersService.Models");
            slnContent.Should().Contain("OrdersService.Core");
            slnContent.Should().Contain("OrdersService.Web");
            slnContent.Should().Contain("OrdersService.Sdk");

            foreach (var module in new[] { "Interfaces", "Models", "Core", "Web", "Sdk" })
            {
                File.Exists(Path.Combine(temp, $"OrdersService.{module}", $"OrdersService.{module}.csproj")).Should().BeTrue();
                File.Exists(Path.Combine(temp, $"OrdersService.{module}", $"{module}Marker.cs")).Should().BeTrue();
            }
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true);
        }
    }
}
