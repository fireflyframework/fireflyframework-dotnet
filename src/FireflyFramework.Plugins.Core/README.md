# FireflyFramework.Plugins.Core

## Overview

`FireflyFramework.Plugins.Core` is the **plugin runtime tier**. It
ships the default `IPluginManager`, `IExtensionRegistry`, and a
hot-reload `AssemblyPluginLoader` that loads plugins from external
DLLs into isolated `AssemblyLoadContext`s using
`McMaster.NETCore.Plugins`. Pair it with
`FireflyFramework.Plugins.Api` (the dependency-light contract tier).

Mirrors `org.fireflyframework:firefly-platform-plugins:plugin-core`
on the Java side. The Java implementation uses an isolated
`URLClassLoader` per plugin; this .NET port uses an isolated
`AssemblyLoadContext` per plugin. The semantics are the same:
plugins ship their own dependency graph and can be loaded and
unloaded without recycling the host process.

## Why a separate module?

Splitting `Api` from `Core` is the standard pattern for any plugin
host (OSGi, JPMS, McMaster). Plugins reference only `Api`, so they
don't drag in the manager, the loader, or the McMaster runtime. The
host references `Core` to get the runtime and the SPI together. Both
sides are insulated from each other's evolution.

## Mental model

```
        Host application
              │
              ▼
   ┌──────────────────────┐
   │ AssemblyPluginLoader │  loads .dll files
   │  (one per assembly)  │  via McMaster.NETCore.Plugins
   └──────────┬───────────┘
              │ instantiates
              ▼
   ┌──────────────────────┐
   │ DefaultPluginManager │  state-machine: Loaded/Initialized/Started/Stopped/Destroyed
   │  (single instance)   │
   └──────────┬───────────┘
              │ owns
              ▼
   ┌──────────────────────────┐
   │ DefaultExtensionRegistry │  priority-ordered registrations
   └──────────────────────────┘
              ▲
              │ register / resolve
   ┌──────────┴───────────┐
   │ Plugin A   Plugin B  │  (each in its own AssemblyLoadContext)
   └──────────────────────┘
```

The single `IExtensionRegistry` instance is the integration seam —
plugins talk to each other through it, but the manager owns its
lifetime so unloads stay clean.

## Public surface

### `DefaultPluginManager`

Reflection-driven manager with a strict state machine
(`Loaded → Initialized → Started → Stopped → Destroyed`).

| Method                          | Behaviour                                                                                |
|---------------------------------|------------------------------------------------------------------------------------------|
| `LoadAsync(pluginType, ct)`     | Validates `[Plugin]`, instantiates via parameterless ctor, runs `InitializeAsync`         |
| `StartAsync(pluginId, ct)`      | Calls `IPlugin.StartAsync`, transitions descriptor to `Started`                          |
| `StopAsync(pluginId, ct)`       | Calls `IPlugin.StopAsync`, transitions descriptor to `Stopped`                           |
| `UnloadAsync(pluginId, ct)`     | Calls `IPlugin.DestroyAsync`, removes the descriptor                                     |
| `GetPlugin(pluginId)`           | Returns the instance or `null`                                                           |
| `GetDescriptor(pluginId)`       | Returns the immutable descriptor or `null`                                                |
| `All()`                         | Snapshot of every descriptor                                                              |
| `Extensions`                    | The shared `IExtensionRegistry`                                                          |

The manager rejects any type that:

- Does not implement `IPlugin`. → `PluginException("does not implement IPlugin")`.
- Lacks a `[Plugin]` attribute. → `PluginException("must be tagged with [Plugin]")`.
- Has no parameterless constructor. → `Activator.CreateInstance` throws.

### `DefaultExtensionRegistry`

Concurrent-dictionary-backed registry. `GetExtension<T>(...)` returns
the single highest-priority extension; `GetExtensions<T>(...)`
returns every extension ordered by priority descending. Equality on
extensions is by reference (delegate identity) so `UnregisterExtension`
must be passed the same instance that was registered.

### `AssemblyPluginLoader`

Loads plugins from external assemblies through
`McMaster.NETCore.Plugins`. Each assembly is loaded into its own
`AssemblyLoadContext` so plugins can ship their own dependency graph
and be unloaded cleanly.

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

| Method                                        | Behaviour                                                          |
|-----------------------------------------------|--------------------------------------------------------------------|
| `LoadFromAssemblyAsync(path, ct)`             | Loads the file, scans for `IPlugin`, calls `manager.LoadAsync` for each |
| `UnloadAsync(pluginId, ct)`                   | Stops + destroys via the manager, disposes the load context        |
| `DisposeAsync()`                              | Disposes every active load context (call from host shutdown)       |

`LoadFromAssemblyAsync` raises `FileNotFoundException` if the path
does not exist and returns an empty list (and disposes the loader)
if the assembly contains no `IPlugin` implementations. The loader
shares two types with the host's load context — `IPlugin` and
`IExtensionRegistry` — so plugin instances cast-check against the
host's contracts rather than the plugin's own copies.

### `IsUnloadable = true`

Each `PluginLoader` is created with `isUnloadable: true`, which
turns on the .NET `AssemblyLoadContext.Unload` path. The loader
disposes the context, then forces a sequence of GC cycles to release
the underlying assembly file lock — without this the operator can't
overwrite the .dll on disk for a hot upgrade.

## Wiring

```csharp
builder.Services.AddSingleton<IExtensionRegistry, DefaultExtensionRegistry>();
builder.Services.AddSingleton<IPluginManager, DefaultPluginManager>();
builder.Services.AddSingleton<AssemblyPluginLoader>();
```

(`FireflyFramework.Starter.Application.AddFireflyApplication` registers
the first two automatically.)

## Common patterns

### Hot reload during development

```csharp
async Task ReloadAsync(string assemblyPath, CancellationToken ct)
{
    // Find any existing plugins from this assembly and unload them
    foreach (var descriptor in manager.All())
    {
        if (Path.GetFileName(assemblyPath).Contains(descriptor.Id, StringComparison.OrdinalIgnoreCase))
        {
            await loader.UnloadAsync(descriptor.Id, ct);
        }
    }

    // Re-load
    var fresh = await loader.LoadFromAssemblyAsync(assemblyPath, ct);
    foreach (var descriptor in fresh)
    {
        await manager.StartAsync(descriptor.Id, ct);
    }
}
```

Pair this with a `FileSystemWatcher` in dev mode to get
true-Erlang-style hot reload during local iteration. **Don't enable
this in production** — the file-watcher race against partial writes
is fragile, and orchestrated rolling deploys are safer.

### Plugin admin endpoint

```csharp
app.MapGet("/admin/plugins", (IPluginManager m) => m.All());

app.MapPost("/admin/plugins/{id}/start", async (string id, IPluginManager m, CancellationToken ct) =>
{
    await m.StartAsync(id, ct);
    return Results.Ok(m.GetDescriptor(id));
});

app.MapPost("/admin/plugins/{id}/stop", async (string id, IPluginManager m, CancellationToken ct) =>
{
    await m.StopAsync(id, ct);
    return Results.Ok(m.GetDescriptor(id));
});
```

Front this with role-based authorisation — random callers shouldn't
be able to stop billing plugins.

### Loading a directory of plugins on startup

```csharp
public sealed class PluginsBootstrapper(
    AssemblyPluginLoader loader,
    IPluginManager manager,
    IConfiguration cfg,
    ILogger<PluginsBootstrapper> log) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        var dir = cfg["Plugins:Directory"] ?? "./plugins";
        if (!Directory.Exists(dir)) return;

        foreach (var dll in Directory.EnumerateFiles(dir, "*.dll").OrderBy(p => p))
        {
            try
            {
                var descriptors = await loader.LoadFromAssemblyAsync(dll, ct);
                foreach (var d in descriptors)
                    await manager.StartAsync(d.Id, ct);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to load plugin assembly {Dll}", dll);
            }
        }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        foreach (var d in manager.All())
            await manager.UnloadAsync(d.Id, ct);
    }
}
```

## Pitfalls and gotchas

- **Shared types are listed explicitly.** The McMaster loader is
  configured with `sharedTypes: [typeof(IPlugin), typeof(IExtensionRegistry)]`.
  If a plugin tries to share a third type (a custom DTO, for
  example) it will get a *different* `Type` instance from the host
  and downcast checks will fail. Add to `sharedTypes` deliberately
  — every shared type couples plugin and host versions.
- **`UnloadAsync` does not always release the .dll immediately.**
  The GC.Collect loop in `AssemblyPluginLoader.UnloadAsync` runs 10
  cycles, which is enough in practice — but objects rooted by
  finalisers, statics, or background threads can defeat unloading.
  If a hot-reload "succeeds" but the .dll stays locked, the plugin
  is leaking a root.
- **Plugins must not register handlers from `LoadAsync`.** The
  framework runs `InitializeAsync` while the descriptor is still
  marked `Loaded`. Defer side-effects until `StartAsync` so an
  `Initialize → Stop → Restart` cycle works cleanly.
- **`DefaultExtensionRegistry` does not enforce contract types at
  registration.** The contract `Type` you pass to
  `RegisterExtensionPoint` is informational; the framework relies on
  the type parameter on `RegisterExtension<T>` to bucket entries.
  Keep the two consistent or you'll get empty result lists.
- **Cancellation tokens are honoured but not enforced.** A plugin
  with a slow `InitializeAsync` will block the manager's `LoadAsync`
  call. Plugins that do real work in `Initialize` should respect the
  CT — host code uses it to time-bound boot.
- **State transitions are linear.** A plugin that's been `Stopped`
  cannot transition straight to `Started`. Either keep plugins
  long-lived for the whole process lifetime, or unload + reload them.

## Internals (for the curious)

- `DefaultPluginManager` stores `(IPlugin, PluginDescriptor)`
  tuples in a `ConcurrentDictionary<string, …>` keyed on plugin id.
  Status updates are atomic via `_plugins[id] = …` writes — readers
  see either the old or the new descriptor, never a torn one.
- `AssemblyPluginLoader._loaders` is keyed on the *first* descriptor
  id from each assembly. This is sufficient when an assembly carries
  one plugin (the common case); for multi-plugin assemblies the
  loader treats them as a unit — unloading any one of them releases
  the shared load context.
- The 10-iteration GC loop is a McMaster idiom: a single
  `GC.Collect()` is rarely enough for finalisable members of an
  unloaded context. The loop is bounded so a stuck unload doesn't
  block the call indefinitely.
- `DefaultExtensionRegistry` keeps registrations in a
  `(string id, Type contract) → List<(int priority, object instance)>`
  shape. Lookups sort by priority on every call — this is fine
  because plugin registrations are rare relative to lookups.

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
