// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FireflyFramework.Testing.Core;

/// <summary>
/// Base class for Firefly tests. Builds a generic host, exposes the IoC
/// container, and offers <see cref="MockBean{T}"/> + <see cref="GetService{T}"/>.
/// </summary>
public abstract class FireflyTestBase : IAsyncDisposable
{
    private IHost? _host;
    private readonly List<Action<IServiceCollection>> _mockBeans = new();
    private IServiceProvider Services => _host?.Services ?? throw new InvalidOperationException("Host not started");

    protected virtual void ConfigureServices(IServiceCollection services) { }

    protected async Task StartAsync()
    {
        var builder = Host.CreateApplicationBuilder(Array.Empty<string>());
        ConfigureServices(builder.Services);
        foreach (var mock in _mockBeans) mock(builder.Services);
        _host = builder.Build();
        await _host.StartAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Replaces the registration for <typeparamref name="T"/> with <paramref name="instance"/>.
    /// Must be called before <see cref="StartAsync"/>.
    /// </summary>
    public void MockBean<T>(T instance) where T : class
    {
        if (_host is not null) throw new InvalidOperationException("Mock beans must be added before StartAsync");
        _mockBeans.Add(services =>
        {
            for (int i = services.Count - 1; i >= 0; i--)
                if (services[i].ServiceType == typeof(T)) services.RemoveAt(i);
            services.AddSingleton(instance);
        });
    }

    public T GetService<T>() where T : notnull => Services.GetRequiredService<T>();
    public IEnumerable<T> GetServices<T>() => Services.GetServices<T>().Where(s => s is not null)!;

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync().ConfigureAwait(false);
            _host.Dispose();
            _host = null;
        }
    }
}
