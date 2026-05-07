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

using FireflyFramework.Plugins.Api;
using McMaster.NETCore.Plugins;
using Microsoft.Extensions.Logging;

namespace FireflyFramework.Plugins.Core;

/// <summary>
/// Loads <see cref="IPlugin"/> implementations from external assemblies via
/// <c>McMaster.NETCore.Plugins</c>. Each assembly is loaded into its own
/// <c>AssemblyLoadContext</c>, so plugins can ship their own dependency graph and be
/// unloaded cleanly. Mirrors Java's plugin classloader isolation.
/// </summary>
public sealed class AssemblyPluginLoader : IAsyncDisposable
{
    private readonly IPluginManager _manager;
    private readonly ILogger<AssemblyPluginLoader> _log;
    private readonly Dictionary<string, PluginLoader> _loaders = new();

    public AssemblyPluginLoader(IPluginManager manager, ILogger<AssemblyPluginLoader> log)
    {
        _manager = manager;
        _log = log;
    }

    /// <summary>
    /// Load every <see cref="IPlugin"/> implementation found in the supplied assembly path
    /// and register it with the manager.
    /// </summary>
    public async Task<IReadOnlyList<PluginDescriptor>> LoadFromAssemblyAsync(string assemblyPath, CancellationToken ct = default)
    {
        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException("Plugin assembly not found", assemblyPath);
        }

        var loader = PluginLoader.CreateFromAssemblyFile(
            assemblyPath,
            sharedTypes: new[] { typeof(IPlugin), typeof(IExtensionRegistry) },
            isUnloadable: true);

        var assembly = loader.LoadDefaultAssembly();
        var descriptors = new List<PluginDescriptor>();
        foreach (var pluginType in assembly.GetTypes()
                     .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IPlugin).IsAssignableFrom(t)))
        {
            var descriptor = await _manager.LoadAsync(pluginType, ct).ConfigureAwait(false);
            descriptors.Add(descriptor);
            _log.LogInformation("Loaded plugin {Plugin} from {Assembly}", descriptor.Id, assemblyPath);
        }

        if (descriptors.Count == 0)
        {
            loader.Dispose();
            return descriptors;
        }

        // Track the loader by the first descriptor id so we can unload later
        _loaders[descriptors[0].Id] = loader;
        return descriptors;
    }

    /// <summary>Unloads the plugin and releases the assembly load context.</summary>
    public async Task UnloadAsync(string pluginId, CancellationToken ct = default)
    {
        await _manager.UnloadAsync(pluginId, ct).ConfigureAwait(false);
        if (_loaders.Remove(pluginId, out var loader))
        {
            loader.Dispose();
            for (var i = 0; i < 10; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var loader in _loaders.Values)
        {
            loader.Dispose();
        }

        _loaders.Clear();
        await Task.CompletedTask;
    }
}
