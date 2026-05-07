// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Reflection;
using FireflyFramework.Actuator.Core;
using Microsoft.Extensions.Hosting;

namespace FireflyFramework.Actuator.Endpoints;

public sealed class InfoEndpoint : IActuatorEndpoint
{
    private readonly IHostEnvironment _env;
    public InfoEndpoint(IHostEnvironment env) { _env = env; }

    public string Id => "info";

    public Task<object?> InvokeAsync(IDictionary<string, string?> parameters, CancellationToken ct)
    {
        var asm = Assembly.GetEntryAssembly();
        return Task.FromResult<object?>(new
        {
            app = new
            {
                name = _env.ApplicationName,
                environment = _env.EnvironmentName,
            },
            build = new
            {
                version = asm?.GetName().Version?.ToString(),
                informationalVersion = asm?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            },
            runtime = new
            {
                framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            },
        });
    }
}
