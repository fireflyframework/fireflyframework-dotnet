// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Session.Adapters;
using FireflyFramework.Session.Configuration;
using FireflyFramework.Session.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FireflyFramework.Session.DependencyInjection;

public static class FireflySessionExtensions
{
    public static IServiceCollection AddFireflySession(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<FireflySessionOptions>().Bind(config.GetSection(FireflySessionOptions.SectionName));
        services.TryAddSingleton<ISessionStore>(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<FireflySessionOptions>>().Value;
            return opt.Provider.Equals("Redis", StringComparison.OrdinalIgnoreCase)
                ? new RedisSessionStore(ConnectionMultiplexer.Connect(opt.Redis.ConnectionString), opt.Redis.KeyPrefix)
                : new InMemorySessionStore();
        });
        return services;
    }
}
