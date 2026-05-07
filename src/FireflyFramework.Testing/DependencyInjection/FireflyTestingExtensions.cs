// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Eda.Publisher;
using FireflyFramework.Testing.Eda;
using Microsoft.Extensions.DependencyInjection;

namespace FireflyFramework.Testing.DependencyInjection;

public static class FireflyTestingExtensions
{
    public static IServiceCollection ReplaceWithCaptureEventPublisher(this IServiceCollection services)
    {
        var existing = services.Where(s => s.ServiceType == typeof(IEventPublisher)).ToList();
        foreach (var d in existing) services.Remove(d);
        var publisher = new EventCapturePublisher();
        services.AddSingleton(publisher);
        services.AddSingleton<IEventPublisher>(publisher);
        return services;
    }

    public static IServiceCollection ReplaceWithMock<TService, TMock>(this IServiceCollection services, TMock mock)
        where TService : class
        where TMock : class, TService
    {
        var existing = services.Where(s => s.ServiceType == typeof(TService)).ToList();
        foreach (var d in existing) services.Remove(d);
        services.AddSingleton<TService>(mock);
        return services;
    }
}
