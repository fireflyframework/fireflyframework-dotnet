// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Actuator.Configuration;
using FireflyFramework.Actuator.Core;
using FireflyFramework.Actuator.Endpoints;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FireflyFramework.Actuator.DependencyInjection;

public static class FireflyActuatorExtensions
{
    public static IServiceCollection AddFireflyActuator(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<FireflyActuatorOptions>().Bind(config.GetSection(FireflyActuatorOptions.SectionName));

        // Snapshot the registrations so BeansEndpoint can introspect them without
        // pulling the live IServiceCollection into the runtime container.
        var snapshot = services.Select(d => new BeansEndpoint.BeanRegistration(
            d.ServiceType.FullName ?? d.ServiceType.Name,
            d.ImplementationType?.FullName,
            d.Lifetime.ToString(),
            d.IsKeyedService)).ToList();
        services.AddSingleton(snapshot);

        services.AddSingleton<IActuatorEndpoint, InfoEndpoint>();
        services.AddSingleton<IActuatorEndpoint, EnvEndpoint>();
        services.AddSingleton<IActuatorEndpoint, BeansEndpoint>();
        services.AddSingleton<IActuatorEndpoint, MetricsEndpoint>();
        services.AddSingleton<IActuatorEndpoint, LoggersEndpoint>();
        services.AddSingleton<IActuatorEndpoint, ThreadDumpEndpoint>();
        services.AddSingleton<IActuatorEndpoint, MappingsEndpoint>();
        return services;
    }

    public static IServiceCollection AddActuatorEndpoint<T>(this IServiceCollection services) where T : class, IActuatorEndpoint
    {
        services.AddSingleton<IActuatorEndpoint, T>();
        return services;
    }
}
