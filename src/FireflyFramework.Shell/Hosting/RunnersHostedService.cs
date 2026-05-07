// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Shell.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FireflyFramework.Shell.Hosting;

/// <summary>Runs every registered <see cref="ICommandLineRunner"/> / <see cref="IApplicationRunner"/> at startup.</summary>
public sealed class RunnersHostedService : IHostedService
{
    private readonly IServiceProvider _provider;
    private readonly ILogger<RunnersHostedService> _logger;

    public RunnersHostedService(IServiceProvider provider, ILogger<RunnersHostedService> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
        var parsed = new ApplicationArguments(args);

        foreach (var runner in _provider.GetServices<ICommandLineRunner>())
        {
            try { await runner.RunAsync(args, cancellationToken).ConfigureAwait(false); }
            catch (Exception ex) { _logger.LogError(ex, "CommandLineRunner {Type} failed", runner.GetType().Name); }
        }
        foreach (var runner in _provider.GetServices<IApplicationRunner>())
        {
            try { await runner.RunAsync(parsed, cancellationToken).ConfigureAwait(false); }
            catch (Exception ex) { _logger.LogError(ex, "ApplicationRunner {Type} failed", runner.GetType().Name); }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
