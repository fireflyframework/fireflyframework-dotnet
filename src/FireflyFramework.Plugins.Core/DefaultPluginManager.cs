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

using System.Collections.Concurrent;
using FireflyFramework.Plugins.Api;
using Microsoft.Extensions.Logging;

namespace FireflyFramework.Plugins.Core;

public sealed class DefaultPluginManager : IPluginManager
{
    private readonly ConcurrentDictionary<string, (IPlugin Plugin, PluginDescriptor Descriptor)> _plugins = new();
    private readonly ILogger<DefaultPluginManager> _log;

    public DefaultPluginManager(ILogger<DefaultPluginManager> log, IExtensionRegistry registry)
    {
        _log = log;
        Extensions = registry;
    }

    public IExtensionRegistry Extensions { get; }

    public async Task<PluginDescriptor> LoadAsync(Type pluginType, CancellationToken ct = default)
    {
        if (!typeof(IPlugin).IsAssignableFrom(pluginType))
        {
            throw new PluginException($"{pluginType.FullName} does not implement IPlugin");
        }

        var attr = pluginType.GetCustomAttributes(typeof(PluginAttribute), false).Cast<PluginAttribute>().FirstOrDefault()
            ?? throw new PluginException($"{pluginType.FullName} must be tagged with [Plugin]");

        var instance = (IPlugin)Activator.CreateInstance(pluginType)!;
        var metadata = instance.Metadata;
        var descriptor = new PluginDescriptor(metadata, PluginState.Loaded, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        await instance.InitializeAsync(ct).ConfigureAwait(false);
        descriptor = descriptor.WithState(PluginState.Initialized);

        _plugins[metadata.Id] = (instance, descriptor);
        _log.LogInformation("Loaded plugin {PluginId} v{Version}", metadata.Id, metadata.Version);
        return descriptor;
    }

    public async Task StartAsync(string pluginId, CancellationToken ct = default)
    {
        if (!_plugins.TryGetValue(pluginId, out var entry))
        {
            throw new PluginException($"Plugin '{pluginId}' is not loaded");
        }

        await entry.Plugin.StartAsync(ct).ConfigureAwait(false);
        _plugins[pluginId] = (entry.Plugin, entry.Descriptor.WithState(PluginState.Started));
    }

    public async Task StopAsync(string pluginId, CancellationToken ct = default)
    {
        if (!_plugins.TryGetValue(pluginId, out var entry)) return;
        await entry.Plugin.StopAsync(ct).ConfigureAwait(false);
        _plugins[pluginId] = (entry.Plugin, entry.Descriptor.WithState(PluginState.Stopped));
    }

    public async Task UnloadAsync(string pluginId, CancellationToken ct = default)
    {
        if (!_plugins.TryRemove(pluginId, out var entry)) return;
        await entry.Plugin.DestroyAsync(ct).ConfigureAwait(false);
    }

    public IPlugin? GetPlugin(string pluginId) => _plugins.TryGetValue(pluginId, out var e) ? e.Plugin : null;
    public PluginDescriptor? GetDescriptor(string pluginId) => _plugins.TryGetValue(pluginId, out var e) ? e.Descriptor : null;
    public IReadOnlyList<PluginDescriptor> All() => _plugins.Values.Select(v => v.Descriptor).ToList();
}
