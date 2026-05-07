# FireflyFramework.Plugins.Api

## Overview

`FireflyFramework.Plugins.Api` is the **plugin SPI tier** — a tiny
dependency-light assembly that defines the lifecycle contract every
Firefly plugin implements. It is the *only* assembly a plugin author
needs to reference; the host application references
`FireflyFramework.Plugins.Core` to get the manager, registry, and
hot-reload assembly loader.

The split between `Api` and `Core` is the same one you see in OSGi
and the JVM `ServiceLoader` patterns — and the same one
`org.fireflyframework:firefly-platform-plugins:plugin-api` enforces on
the Java side. It exists for two reasons:

1. **Plugins must not transitively load the host's implementation.**
   If `Plugins.Api` depended on `Plugins.Core`, every plugin would
   pull in `McMaster.NETCore.Plugins`, the manager, and the
   reflection helpers — each duplicated under the plugin's isolated
   load context. Keeping `Api` lean keeps plugins lean.
2. **A plugin author can be written against a single, stable contract.**
   The host can swap `DefaultPluginManager` for a custom
   implementation without breaking plugins.

## Why a separate module?

A platform that hosts third-party extensions has three competing
concerns:

- **Lifecycle.** Plugins need ordered initialise → start → stop →
  destroy hooks so they can claim resources cleanly.
- **Extension registration.** Plugins want to publish callable
  contributions that the host (or other plugins) consume — without
  hardwiring class names.
- **Isolation.** A misbehaving plugin must not crash the host or
  poison its assembly graph; it must be unloadable.

`Plugins.Api` covers the first two. It defines `IPlugin` (lifecycle),
`IExtensionRegistry` (registration), and the `[Plugin]` attribute
(metadata discovery). Isolation is a runtime concern handled by
`Plugins.Core`'s `AssemblyPluginLoader`.

## Mental model

```
                Host application
                       │
                       ▼
               ┌───────────────────┐
               │ IPluginManager    │  (provided by Plugins.Core)
               └────┬─────────┬────┘
                    │         │
                    │         └── owns ──► IExtensionRegistry
                    │
        ┌───────────┴────────────┐
        │ Loaded   Initialized   │
        │ Started  Stopped       │     state machine
        │ Destroyed              │
        └────────────┬───────────┘
                     │
       ┌─────────────┼─────────────┐
       │             │             │
   ┌───▼────┐   ┌────▼───┐   ┌─────▼────┐
   │ Plugin │   │ Plugin │   │  Plugin  │
   │ A      │   │ B      │   │  C       │
   │ [IPlug]│   │ [IPlug]│   │ [IPlug]  │
   └────────┘   └────────┘   └──────────┘
        │            │            │
        └────────────┴────────────┘
                     │
              register & resolve
                     │
              ┌──────▼─────────┐
              │ extension      │
              │ "greeting"     │
              │ Func<string>   │
              │   • plugin A → │
              │   • plugin B → │
              └────────────────┘
```

The state machine is intentional. A plugin can't start before it has
initialised (resources may not be ready); it can't be unloaded
before it has stopped (handlers may still be in use).

## Public surface

### `IPlugin`

```csharp
public interface IPlugin
{
    PluginMetadata Metadata { get; }

    Task InitializeAsync(CancellationToken ct = default);
    Task StartAsync     (CancellationToken ct = default);
    Task StopAsync      (CancellationToken ct = default);
    Task DestroyAsync   (CancellationToken ct = default);
}
```

| Method            | When the manager calls it                                                 |
|-------------------|---------------------------------------------------------------------------|
| `InitializeAsync` | After `LoadAsync` — claim non-runtime resources, register extensions      |
| `StartAsync`      | When the operator (or container) starts the plugin — claim runtime hooks |
| `StopAsync`       | Before unload — release runtime hooks, finish in-flight work             |
| `DestroyAsync`    | After unload — release everything else, drop subscriptions               |

The state transitions enforced by `DefaultPluginManager` ensure each
of those is called at most once, in order.

### `PluginMetadata`

Immutable record carrying:

| Field          | Purpose                                                 |
|----------------|---------------------------------------------------------|
| `Id`           | Stable plugin id (used in registry lookups, logs)       |
| `Name`         | Human-readable name                                     |
| `Version`      | Plugin version (independent of host or framework)       |
| `Description`  | Optional one-liner shown in admin UIs                   |
| `Author`       | Optional contact / org                                  |
| `Dependencies` | List of plugin ids that must load before this one       |

`Dependencies` is consumed by the host's load orchestrator if you
have one — the framework doesn't auto-resolve a plugin graph. For
small deployments (5-10 plugins) ordered loading from a config list
is plenty; for larger deployments you'd build a topological sort over
`Dependencies`.

### `PluginDescriptor`

Wraps `PluginMetadata` with the runtime state machine:
`Loaded → Initialized → Started → Stopped → Destroyed`.

```csharp
public sealed record PluginDescriptor(
    PluginMetadata Metadata,
    PluginState    State,
    DateTimeOffset LoadedAt,
    DateTimeOffset LastStateChange);
```

`WithState(PluginState)` returns a new descriptor with the new state
and `LastStateChange` bumped. The descriptor is immutable so
concurrent reads from a status endpoint are race-free.

### `IPluginManager`

```csharp
public interface IPluginManager
{
    Task<PluginDescriptor> LoadAsync     (Type pluginType, CancellationToken ct = default);
    Task                   StartAsync    (string pluginId, CancellationToken ct = default);
    Task                   StopAsync     (string pluginId, CancellationToken ct = default);
    Task                   UnloadAsync   (string pluginId, CancellationToken ct = default);
    IPlugin?               GetPlugin     (string pluginId);
    PluginDescriptor?      GetDescriptor (string pluginId);
    IReadOnlyList<PluginDescriptor> All();
    IExtensionRegistry     Extensions    { get; }
}
```

`LoadAsync` instantiates the plugin via its parameterless constructor,
reads metadata from `[Plugin]` (or `IPlugin.Metadata`), calls
`InitializeAsync`, and stores the descriptor.

`UnloadAsync` calls `StopAsync` if the plugin is still running, then
`DestroyAsync`, and removes the descriptor.

### `IExtensionRegistry`

Plugins register and resolve extension points keyed by id and
contract type. Lookups are priority-ordered.

```csharp
registry.RegisterExtensionPoint("greeting", typeof(Func<string>));
registry.RegisterExtension<Func<string>>("greeting", () => "hello",  priority: 1);
registry.RegisterExtension<Func<string>>("greeting", () => "namaste", priority: 10);

var top  = registry.GetExtension<Func<string>>("greeting")!();    // "namaste"
var all  = registry.GetExtensions<Func<string>>("greeting");       // ordered: "namaste" then "hello"
```

| Method                              | Behaviour                                                  |
|-------------------------------------|------------------------------------------------------------|
| `RegisterExtensionPoint(id, type)`  | Declares the contract; calling twice with a mismatched type throws |
| `RegisterExtension<T>(id, ext, p)`  | Registers under priority `p` (higher = earlier)            |
| `UnregisterExtension<T>(id, ext)`   | Removes by reference — typically called from `StopAsync`   |
| `GetExtension<T>(id)`               | Returns the highest-priority extension or `null`           |
| `GetExtensions<T>(id)`              | Returns every extension, ordered descending priority       |
| `GetExtensionPointIds()`            | Lists registered point ids — used by admin UI              |
| `HasExtensionPoint(id)`             | True iff the point id was previously registered            |

### `[Plugin]`

```csharp
[Plugin("greeter", "Greeter Plugin", "1.0.0")]
public sealed class GreeterPlugin : IPlugin
{
    public PluginMetadata Metadata =>
        new("greeter", "Greeter Plugin", "1.0.0",
            Description: "Says hello.",
            Author:      "Ada Lovelace",
            Dependencies: Array.Empty<string>());

    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task StartAsync     (CancellationToken ct = default) => Task.CompletedTask;
    public Task StopAsync      (CancellationToken ct = default) => Task.CompletedTask;
    public Task DestroyAsync   (CancellationToken ct = default) => Task.CompletedTask;
}
```

The attribute is purely for discovery — the manager prefers the
runtime `Metadata` property when both are present. Use the attribute
when reflection-only metadata access matters (e.g. listing plugins
without instantiating them) and the property when you need
interpolated values that aren't compile-time constants.

### `PluginException`

Typed `FireflyException` with stable error code `PLUGIN_ERROR`. Throw
from a plugin's `InitializeAsync` / `StartAsync` to signal a
fail-fast condition (missing config, permission denied) — the manager
will roll the descriptor back to `Loaded` and the operator can
inspect the cause via `GetDescriptor`.

## Common patterns

### Discovering and loading plugins from a directory

```csharp
foreach (var dll in Directory.EnumerateFiles("./plugins", "*.dll"))
{
    foreach (var descriptor in await loader.LoadFromAssemblyAsync(dll, ct))
    {
        await manager.StartAsync(descriptor.Id, ct);
    }
}
```

For a more controlled deployment, list the plugin assemblies
explicitly in configuration and load only those.

### Publishing an extension from a plugin

```csharp
public sealed class CurrencyConversionPlugin : IPlugin
{
    private readonly Func<decimal, string, decimal> _convert =
        (amount, ccy) => /* call external rate API */;

    public PluginMetadata Metadata { get; } =
        new("currency", "Currency Conversion", "1.0.0", null, null, Array.Empty<string>());

    public Task InitializeAsync(CancellationToken ct)
    {
        registry.RegisterExtensionPoint("currency.convert", typeof(Func<decimal, string, decimal>));
        registry.RegisterExtension("currency.convert", _convert, priority: 50);
        return Task.CompletedTask;
    }

    public Task StartAsync (CancellationToken ct) => Task.CompletedTask;

    public Task StopAsync(CancellationToken ct)
    {
        registry.UnregisterExtension("currency.convert", _convert);
        return Task.CompletedTask;
    }

    public Task DestroyAsync(CancellationToken ct) => Task.CompletedTask;
}
```

### Consuming extensions from host code

```csharp
var converter = manager.Extensions
    .GetExtension<Func<decimal, string, decimal>>("currency.convert");

if (converter is null)
{
    return Problem("No currency converter plugin is loaded.");
}

var inUsd = converter(amount, "USD");
```

For a *chain* of extensions (e.g. a stack of validators), iterate
`GetExtensions<T>` in priority order:

```csharp
foreach (var validator in manager.Extensions.GetExtensions<Func<Order, ValidationResult>>("order.validate"))
{
    var result = validator(order);
    if (!result.IsValid) return BadRequest(result.Errors);
}
```

## Pitfalls and gotchas

- **Parameterless constructor required.** `DefaultPluginManager`
  instantiates via `Activator.CreateInstance(pluginType)`. If your
  plugin needs services, accept them through a static
  `ServiceProvider` or via a `[Plugin]`-decorated factory plugin that
  instantiates the rest from an `IServiceProvider` it cached during
  `InitializeAsync`.
- **Don't swallow exceptions in `InitializeAsync`.** Throw them — the
  manager catches `PluginException` and surfaces them on the
  descriptor. Swallowing leaves a half-initialised plugin in the
  registry.
- **`PluginMetadata.Dependencies` is advisory.** The framework
  doesn't load them transitively; your bootstrap code must respect
  the order. Either feed plugins to the manager in topo order or
  layer your own resolver on top.
- **Extension registrations leak across reload.** Always call
  `UnregisterExtension` from `StopAsync`. The manager will not do it
  for you because extensions may legitimately outlive the plugin
  that registered them (e.g. if the plugin was a one-shot
  installer).
- **`PluginState` transitions are linear.** A plugin can't go from
  `Stopped` back to `Started` directly — call `LoadAsync` again on a
  fresh descriptor. This is intentional: it ensures `InitializeAsync`
  always runs against fresh in-memory state.

## Internals (for the curious)

- `PluginDescriptor.WithState(...)` is implemented via the C# record
  `with` expression so it's compile-time-checked and allocation-light.
- `IExtensionRegistry`'s priority is a simple `int` — the framework
  doesn't impose a scale. Convention in the codebase: 0 for the
  framework default, 50 for normal plugins, 100+ for explicit
  override-the-default.
- The `T?` return on `GetExtension<T>(id)` is intentional. Treating
  "no extension" as a normal control-flow case lets host code fall
  through to a default behaviour without try/catch noise.

## Dependencies

| Reference                  | Used for                |
|----------------------------|-------------------------|
| `FireflyFramework.Kernel`  | `FireflyException` base |

## Java mapping

| .NET                  | Java                              |
|-----------------------|-----------------------------------|
| `IPlugin`             | `Plugin`                          |
| `PluginMetadata`      | `PluginMetadata`                  |
| `PluginDescriptor`    | `PluginDescriptor`                |
| `PluginState`         | `PluginState`                     |
| `IPluginManager`      | `PluginManager`                   |
| `IExtensionRegistry`  | `ExtensionRegistry`               |
| `[Plugin]`            | `@Plugin`                         |
| `PluginException`     | `PluginException`                 |
