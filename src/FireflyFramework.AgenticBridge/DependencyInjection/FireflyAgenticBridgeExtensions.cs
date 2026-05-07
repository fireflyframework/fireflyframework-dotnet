// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.AgenticBridge.Adapters;
using FireflyFramework.AgenticBridge.Configuration;
using FireflyFramework.AgenticBridge.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FireflyFramework.AgenticBridge.DependencyInjection;

public static class FireflyAgenticBridgeExtensions
{
    public static IServiceCollection AddFireflyAgenticBridge(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<FireflyAgenticBridgeOptions>().Bind(config.GetSection(FireflyAgenticBridgeOptions.SectionName));

        services.AddHttpClient<IAgenticClient, RestAgenticClient>((sp, http) =>
        {
            var opt = sp.GetRequiredService<IOptions<FireflyAgenticBridgeOptions>>().Value;
            http.BaseAddress = new Uri(opt.BaseUrl.TrimEnd('/') + "/");
            http.Timeout = opt.RequestTimeout;
            if (!string.IsNullOrEmpty(opt.ApiKey)) http.DefaultRequestHeaders.Add("X-Api-Key", opt.ApiKey);
        }).AddStandardResilienceHandler();

        return services;
    }
}
