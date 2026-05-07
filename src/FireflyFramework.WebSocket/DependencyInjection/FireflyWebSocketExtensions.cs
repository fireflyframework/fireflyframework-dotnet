// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.WebSocket.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FireflyFramework.WebSocket.DependencyInjection;

public static class FireflyWebSocketExtensions
{
    public static IServiceCollection AddFireflyWebSockets(this IServiceCollection services)
    {
        services.TryAddSingleton<IWebSocketSessionRegistry, WebSocketSessionRegistry>();
        return services;
    }

    public static IServiceCollection AddWebSocketHandler<T>(this IServiceCollection services) where T : class, IWebSocketHandler
    {
        services.AddSingleton<IWebSocketHandler, T>();
        return services;
    }
}
