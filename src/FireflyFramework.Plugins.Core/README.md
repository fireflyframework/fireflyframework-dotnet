# FireflyFramework.Plugins.Core

Default implementations of the plugin SPI defined in
`FireflyFramework.Plugins.Api`, plus a hot-reload assembly loader.

Mirrors `org.fireflyframework:firefly-platform-plugins:plugin-core`.

## Public surface

### `DefaultPluginManager`

Reflection-driven manager with a strict state machine
(`Loaded → Initialized → Started → Stopped → Destroyed`).
`LoadAsync(pluginType)` reads the `[Plugin]` attribute, instantiates the
type via the parameterless constructor, calls `InitializeAsync`, and
records the descriptor.

### `DefaultExtensionRegistry`

Concurrent-dictionary-backed registry. `GetExtension<T>(...)` returns the
single highest-priority extension; `GetExtensions<T>(...)` returns every
extension ordered by priority descending.

### `AssemblyPluginLoader`

Loads plugins from external assemblies through
`McMaster.NETCore.Plugins`. Each assembly is loaded into its own
`AssemblyLoadContext` so plugins can ship their own dependency graph and
be unloaded cleanly without recycling the host process.

```csharp
using FireflyFramework.Plugins.Api;
using FireflyFramework.Plugins.Core;

await using var loader = new AssemblyPluginLoader(manager, logger);

// Discover and load every IPlugin in the supplied assembly:
var descriptors = await loader.LoadFromAssemblyAsync("./plugins/greeter.dll", ct);

// Start them all:
foreach (var descriptor in descriptors)
{
    await manager.StartAsync(descriptor.Id, ct);
}

// Hot-unload (releases the assembly load context):
await loader.UnloadAsync(descriptors[0].Id, ct);
```

`LoadFromAssemblyAsync` raises `FileNotFoundException` if the path does
not exist and `PluginException` if the assembly contains no `IPlugin`
implementations.

## Wiring

```csharp
builder.Services.AddSingleton<IExtensionRegistry, DefaultExtensionRegistry>();
builder.Services.AddSingleton<IPluginManager, DefaultPluginManager>();
builder.Services.AddSingleton<AssemblyPluginLoader>();
```

(`FireflyFramework.Starter.Application.AddFireflyApplication` registers
the first two automatically.)

## Dependencies

| Reference                                | Used for                     |
|------------------------------------------|------------------------------|
| `FireflyFramework.Plugins.Api`           | SPI contracts                |
| `McMaster.NETCore.Plugins`               | Isolated assembly loading    |
| `Microsoft.Extensions.Logging.Abstractions` | Manager / loader logging  |

## Java mapping

| .NET                          | Java                                              |
|-------------------------------|---------------------------------------------------|
| `DefaultPluginManager`        | `DefaultPluginManager`                            |
| `DefaultExtensionRegistry`    | `DefaultExtensionRegistry`                        |
| `AssemblyPluginLoader`        | `PluginClassLoader` + `PluginRegistry` discovery  |
