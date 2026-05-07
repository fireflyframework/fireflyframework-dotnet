// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Resilience.Configuration;
using FireflyFramework.Resilience.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace FireflyFramework.Resilience.DependencyInjection;

public static class FireflyResilienceExtensions
{
    public static IServiceCollection AddFireflyResilience(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<FireflyResilienceOptions>().Bind(config.GetSection(FireflyResilienceOptions.SectionName));
        services.TryAddSingleton<IResilienceRegistry>(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<FireflyResilienceOptions>>().Value;
            var reg = new DefaultResilienceRegistry();
            foreach (var (n, o) in opt.CircuitBreakers) reg.Register(n, ResiliencePipelineFactory.BuildCircuitBreaker(n, o));
            foreach (var (n, o) in opt.Retries) reg.Register(n, ResiliencePipelineFactory.BuildRetry(n, o));
            foreach (var (n, o) in opt.RateLimiters) reg.Register(n, ResiliencePipelineFactory.BuildRateLimiter(n, o));
            foreach (var (n, o) in opt.Bulkheads) reg.Register(n, ResiliencePipelineFactory.BuildBulkhead(n, o));
            foreach (var (n, o) in opt.TimeLimiters) reg.Register(n, ResiliencePipelineFactory.BuildTimeLimiter(n, o));
            return reg;
        });
        return services;
    }
}
