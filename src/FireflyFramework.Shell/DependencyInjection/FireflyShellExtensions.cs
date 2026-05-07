// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Shell.Core;
using FireflyFramework.Shell.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FireflyFramework.Shell.DependencyInjection;

public static class FireflyShellExtensions
{
    public static IServiceCollection AddFireflyShell(this IServiceCollection services)
    {
        services.TryAddSingleton<IShellRunner, DefaultShellRunner>();
        services.AddHostedService<RunnersHostedService>();
        return services;
    }

    public static IServiceCollection AddShellComponent<T>(this IServiceCollection services) where T : class, IFireflyShellComponent
    {
        services.AddSingleton<IFireflyShellComponent, T>();
        services.AddSingleton<T>();
        return services;
    }

    public static IServiceCollection AddCommandLineRunner<T>(this IServiceCollection services) where T : class, ICommandLineRunner
    {
        services.AddSingleton<ICommandLineRunner, T>();
        return services;
    }

    public static IServiceCollection AddApplicationRunner<T>(this IServiceCollection services) where T : class, IApplicationRunner
    {
        services.AddSingleton<IApplicationRunner, T>();
        return services;
    }
}
