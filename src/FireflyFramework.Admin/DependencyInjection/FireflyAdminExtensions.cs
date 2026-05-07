// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Admin.Client;
using FireflyFramework.Admin.Configuration;
using FireflyFramework.Admin.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FireflyFramework.Admin.DependencyInjection;

public static class FireflyAdminExtensions
{
    public static IServiceCollection AddFireflyAdminServer(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<FireflyAdminServerOptions>().Bind(config.GetSection(FireflyAdminServerOptions.SectionName));
        services.TryAddSingleton<IInstanceRegistry, InMemoryInstanceRegistry>();
        return services;
    }

    public static IServiceCollection AddFireflyAdminClient(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<FireflyAdminClientOptions>().Bind(config.GetSection(FireflyAdminClientOptions.SectionName));
        services.AddHttpClient("firefly-admin");
        services.AddHostedService<AdminClientHostedService>();
        return services;
    }
}
