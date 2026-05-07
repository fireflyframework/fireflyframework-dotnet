// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Shell.Annotations;
using FireflyFramework.Shell.Core;

namespace FireflyFramework.Cli.Commands;

[ShellComponent]
public sealed class MigrationCommand : IFireflyShellComponent
{
    [ShellMethod(Name = "migration", Description = "Create a Flyway-style timestamped migration file")]
    public Task Migration(
        [ShellArgument(Name = "name", Description = "Migration name (snake_case)")] string name,
        [ShellOption(Long = "out", DefaultValue = "src/main/resources/db/migration")] string outDir)
    {
        Directory.CreateDirectory(outDir);
        var version = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var path = Path.Combine(outDir, $"V{version}__{name}.sql");
        File.WriteAllText(path, $"-- Migration: {name}\n-- Created: {DateTime.UtcNow:O}\n\n");
        Console.WriteLine($"created migration at {path}");
        return Task.CompletedTask;
    }
}
