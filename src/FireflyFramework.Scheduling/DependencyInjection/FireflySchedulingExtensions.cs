// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Scheduling.Core;
using FireflyFramework.Scheduling.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace FireflyFramework.Scheduling.DependencyInjection;

public static class FireflySchedulingExtensions
{
    public static IServiceCollection AddFireflyScheduling(this IServiceCollection services)
    {
        services.TryAddSingleton<ITaskScheduler, CronosTaskScheduler>();
        services.TryAddSingleton<ITaskExecutor, TaskPoolExecutor>();
        services.AddHostedService<ScheduledMethodHostedService>();
        return services;
    }

    public static IServiceCollection AddScheduledHost<T>(this IServiceCollection services) where T : class, IScheduledTaskHost
    {
        services.AddSingleton<IScheduledTaskHost, T>();
        services.AddSingleton<T>();
        return services;
    }
}
