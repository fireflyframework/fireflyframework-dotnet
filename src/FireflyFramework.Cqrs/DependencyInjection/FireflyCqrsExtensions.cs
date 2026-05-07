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
