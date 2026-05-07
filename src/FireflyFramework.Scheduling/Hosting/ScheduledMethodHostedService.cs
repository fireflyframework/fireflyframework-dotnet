// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using System.Reflection;
using FireflyFramework.Scheduling.Annotations;
using FireflyFramework.Scheduling.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FireflyFramework.Scheduling.Hosting;

/// <summary>
/// Discovers all <see cref="ScheduledAttribute"/>-decorated methods on registered
/// singletons and arms them on the <see cref="ITaskScheduler"/> when the host starts.
/// </summary>
public sealed class ScheduledMethodHostedService : IHostedService
{
    private readonly IServiceProvider _provider;
    private readonly ITaskScheduler _scheduler;
    private readonly ILogger<ScheduledMethodHostedService> _logger;

    public ScheduledMethodHostedService(IServiceProvider provider, ITaskScheduler scheduler, ILogger<ScheduledMethodHostedService> logger)
    {
        _provider = provider;
        _scheduler = scheduler;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var bean in EnumerateSingletons())
        {
            var beanType = bean.GetType();
            foreach (var method in beanType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
            {
                var attr = method.GetCustomAttribute<ScheduledAttribute>();
                if (attr is null) continue;

                Func<CancellationToken, Task> action = ct => InvokeAsync(bean, method, ct);
                var id = $"{beanType.Name}.{method.Name}";

                if (!string.IsNullOrEmpty(attr.Cron))
                {
                    var tz = string.IsNullOrEmpty(attr.Zone) ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(attr.Zone);
                    _scheduler.ScheduleCron(attr.Cron, action, id, tz);
                }
                else if (attr.FixedRate > TimeSpan.Zero)
                {
                    _scheduler.ScheduleAtFixedRate(attr.FixedRate, action, attr.InitialDelay, id);
                }
                else if (attr.FixedDelay > TimeSpan.Zero)
                {
                    _scheduler.ScheduleWithFixedDelay(attr.FixedDelay, action, attr.InitialDelay, id);
                }

                _logger.LogInformation("Registered scheduled method {Id}", id);
            }
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var t in _scheduler.GetAll()) _scheduler.Cancel(t.Id);
        return Task.CompletedTask;
    }

    private IEnumerable<object> EnumerateSingletons() =>
        _provider.GetServices<IScheduledTaskHost>().Cast<object>();

    private static Task InvokeAsync(object target, MethodInfo m, CancellationToken ct)
    {
        var args = m.GetParameters().Length switch
        {
            0 => Array.Empty<object?>(),
            1 when m.GetParameters()[0].ParameterType == typeof(CancellationToken) => new object?[] { ct },
            _ => new object?[m.GetParameters().Length],
        };
        var result = m.Invoke(target, args);
        return result switch
        {
            Task t => t,
            ValueTask vt => vt.AsTask(),
            _ => Task.CompletedTask,
        };
    }
}

/// <summary>Marker interface for DI discovery of components hosting <see cref="ScheduledAttribute"/> methods.</summary>
public interface IScheduledTaskHost { }
