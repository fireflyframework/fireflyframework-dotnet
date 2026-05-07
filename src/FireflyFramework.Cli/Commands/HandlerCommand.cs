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
public sealed class HandlerCommand : IFireflyShellComponent
{
    [ShellMethod(Name = "handler", Description = "Generate a CQRS command/query handler")]
    public Task Handler(
        [ShellArgument(Name = "kind", Description = "command|query")] string kind,
        [ShellArgument(Name = "name", Description = "Handler name (PascalCase)")] string name,
        [ShellOption(Long = "out", DefaultValue = ".")] string outDir)
    {
        if (!kind.Equals("command", StringComparison.OrdinalIgnoreCase) && !kind.Equals("query", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("kind must be 'command' or 'query'");

        var path = Path.Combine(outDir, "Handlers", $"{name}Handler.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, HandlerScaffold.Render(name, kind.ToLowerInvariant()));
        Console.WriteLine($"created {kind} handler at {path}");
        return Task.CompletedTask;
    }
}
