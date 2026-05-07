// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Cli.Commands;
using FireflyFramework.Shell.Core;
using FireflyFramework.Shell.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddFireflyShell()
    .AddShellComponent<NewServiceCommand>()
    .AddShellComponent<HandlerCommand>()
    .AddShellComponent<SagaCommand>()
    .AddShellComponent<MigrationCommand>()
    .AddShellComponent<HelpCommand>();

using var host = builder.Build();
var runner = host.Services.GetRequiredService<IShellRunner>();

if (args.Length == 0)
{
    await runner.RunOnceAsync(new[] { "help" }, CancellationToken.None).ConfigureAwait(false);
    return 0;
}

return await runner.RunOnceAsync(args, CancellationToken.None).ConfigureAwait(false);
