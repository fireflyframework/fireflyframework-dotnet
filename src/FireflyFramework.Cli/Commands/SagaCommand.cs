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
public sealed class SagaCommand : IFireflyShellComponent
{
    [ShellMethod(Name = "saga", Description = "Generate a saga skeleton with placeholder steps")]
    public Task Saga(
        [ShellArgument(Name = "name", Description = "Saga name (PascalCase)")] string name,
        [ShellOption(Long = "out", DefaultValue = ".")] string outDir,
        [ShellOption(Long = "steps", DefaultValue = "3")] int steps)
    {
        var path = Path.Combine(outDir, "Sagas", $"{name}Saga.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, SagaScaffold.Render(name, steps));
        Console.WriteLine($"created saga at {path}");
        return Task.CompletedTask;
    }
}
