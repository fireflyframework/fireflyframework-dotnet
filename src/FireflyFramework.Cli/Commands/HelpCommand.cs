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
public sealed class HelpCommand : IFireflyShellComponent
{
    [ShellMethod(Name = "help", Description = "Show available commands")]
    public Task Help()
    {
        Console.WriteLine("firefly — .NET scaffolding CLI for the Firefly framework");
        Console.WriteLine();
        Console.WriteLine("Usage: firefly <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  new <name> --tier=core|domain|experience       Scaffold a new microservice");
        Console.WriteLine("  handler <command|query> <Name>                 Generate a CQRS handler");
        Console.WriteLine("  saga <Name> --steps=N                          Generate a saga skeleton");
        Console.WriteLine("  migration <name>                               Create a Flyway-style migration file");
        Console.WriteLine("  help                                           Show this message");
        return Task.CompletedTask;
    }
}
