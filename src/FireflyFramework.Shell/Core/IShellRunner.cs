// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.Shell.Core;

public interface IShellRunner
{
    Task<int> RunOnceAsync(IReadOnlyList<string> args, CancellationToken ct);
    Task RunInteractiveAsync(CancellationToken ct);
}

/// <summary>
/// Spring <c>CommandLineRunner</c> port. Runs once at startup, before the main
/// host enters its idle loop, with the host's argv.
/// </summary>
public interface ICommandLineRunner
{
    Task RunAsync(string[] args, CancellationToken ct);
}

/// <summary>Spring <c>ApplicationRunner</c> port. Same as the above but receives parsed args.</summary>
public interface IApplicationRunner
{
    Task RunAsync(IApplicationArguments args, CancellationToken ct);
}

public interface IApplicationArguments
{
    IReadOnlyList<string> SourceArgs { get; }
    IReadOnlyList<string> NonOptionArgs { get; }
    IReadOnlyDictionary<string, string?> OptionArgs { get; }
    bool ContainsOption(string name);
}
