// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Aop.Annotations;
using FireflyFramework.Aop.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FireflyFramework.Aop.DependencyInjection;

public static class FireflyAopExtensions
{
    public static IServiceCollection AddFireflyAop(this IServiceCollection services)
    {
        services.TryAddSingleton<IAspectRegistry>(sp =>
        {
            var aspects = sp.GetServices<IFireflyAspect>().Cast<object>();
            return AspectRegistry.FromAspects(aspects);
        });
        return services;
    }

    public static IServiceCollection AddAspect<T>(this IServiceCollection services) where T : class, IFireflyAspect
    {
        services.AddSingleton<IFireflyAspect, T>();
        return services;
    }
}

/// <summary>Marker interface for DI discovery; aspects also wear <see cref="AspectAttribute"/>.</summary>
public interface IFireflyAspect { }
