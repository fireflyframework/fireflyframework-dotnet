// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Agentic.Core;
using FireflyFramework.Agentic.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FireflyFramework.Agentic.DependencyInjection;

public static class FireflyAgenticExtensions
{
    public static IServiceCollection AddFireflyAgentic(this IServiceCollection services)
    {
        services.TryAddSingleton<IAgentMemory>(_ => new WindowedMemory());
        return services;
    }

    public static IServiceCollection AddAgentTool<T>(this IServiceCollection services) where T : class, IAgentTool
    {
        services.AddSingleton<IAgentTool, T>();
        return services;
    }
}
