// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Reflection;
using FireflyFramework.Cqrs.Buses;
using FireflyFramework.Cqrs.Commands;
using FireflyFramework.Cqrs.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace FireflyFramework.Cqrs.DependencyInjection;

/// <summary>
/// DI registration. Replaces Spring's <c>CqrsAutoConfiguration</c>: scans the supplied
/// assemblies for command/query handlers and registers them against their generic
/// closed types so the buses can resolve them.
/// </summary>
public static class FireflyCqrsExtensions
{
    public static IServiceCollection AddFireflyCqrs(
        this IServiceCollection services,
        params Assembly[] handlerAssemblies)
    {
        services.AddSingleton<ICommandBus, DefaultCommandBus>();
        services.AddSingleton<IQueryBus, DefaultQueryBus>();
        RegisterHandlers(services, handlerAssemblies);
        return services;
    }

    private static void RegisterHandlers(IServiceCollection services, Assembly[] assemblies)
    {
        var commandHandlerOpen = typeof(ICommandHandler<,>);
        var queryHandlerOpen = typeof(IQueryHandler<,>);

        foreach (var asm in assemblies)
        {
            foreach (var type in asm.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }))
            {
                foreach (var iface in type.GetInterfaces())
                {
                    if (!iface.IsGenericType)
                    {
                        continue;
                    }

                    var def = iface.GetGenericTypeDefinition();
                    if (def == commandHandlerOpen || def == queryHandlerOpen)
                    {
                        services.AddTransient(iface, type);
                    }
                }
            }
        }
    }
}
