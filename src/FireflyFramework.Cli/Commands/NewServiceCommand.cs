// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Cli.Templates;
using FireflyFramework.Shell.Annotations;
using FireflyFramework.Shell.Core;

namespace FireflyFramework.Cli.Commands;

[ShellComponent]
public sealed class NewServiceCommand : IFireflyShellComponent
{
    [ShellMethod(Name = "new", Description = "Scaffold a new Firefly microservice")]
    public Task NewService(
        [ShellArgument(Name = "name", Description = "Service name (kebab-case)")] string name,
        [ShellOption(Long = "tier", Description = "core|domain|experience", DefaultValue = "core")] string tier,
        [ShellOption(Long = "out", Description = "Output directory", DefaultValue = ".")] string outDir,
        [ShellOption(Long = "namespace", Description = ".NET root namespace")] string? rootNamespace = null)
    {
        var ns = rootNamespace ?? ToPascal(name);
        var target = Path.Combine(outDir, name);
        Directory.CreateDirectory(target);

        var ctx = new TemplateContext(name, ns, tier);
        ServiceScaffold.Render(target, ctx);
        Console.WriteLine($"created service {name} ({tier}) under {target}");
        return Task.CompletedTask;
    }

    private static string ToPascal(string s) =>
        string.Concat(s.Split('-', '_').Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
}
