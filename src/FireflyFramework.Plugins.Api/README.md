# FireflyFramework.Plugins.Api

Plugin SPI — pure interfaces and metadata records, no implementation
dependencies. Reference this from a plugin assembly to expose the
`IPlugin` contract; reference `FireflyFramework.Plugins.Core` from the
host to get the manager and loader.

Mirrors `org.fireflyframework:firefly-platform-plugins:plugin-api`.

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

### `PluginMetadata`

Immutable record carrying `Id`, `Name`, `Version`, optional `Description`,
optional `Author`, and `Dependencies` (a list of plugin ids that must
load first).

### `PluginDescriptor`

Wraps `PluginMetadata` with the runtime state machine: `Loaded` →
`Initialized` → `Started` → `Stopped` → `Destroyed`. `WithState(...)`
returns a copy with the new state and an updated `LastStateChange`
timestamp.

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

### `IExtensionRegistry`

Plugins register and resolve extension points keyed by id and contract
type. Lookups are priority-ordered.

```csharp
registry.RegisterExtensionPoint("greeting", typeof(Func<string>));
registry.RegisterExtension<Func<string>>("greeting", () => "hello",  priority: 1);
registry.RegisterExtension<Func<string>>("greeting", () => "namaste", priority: 10);

var top  = registry.GetExtension<Func<string>>("greeting")!();   // "namaste"
var all  = registry.GetExtensions<Func<string>>("greeting");      // ordered, "namaste" then "hello"
```

### `[Plugin]`

```csharp
[Plugin("greeter", "Greeter Plugin", "1.0.0")]
public sealed class GreeterPlugin : IPlugin
{
    public PluginMetadata Metadata =>
        new("greeter", "Greeter Plugin", "1.0.0", null, null, Array.Empty<string>());

    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task StartAsync     (CancellationToken ct = default) => Task.CompletedTask;
    public Task StopAsync      (CancellationToken ct = default) => Task.CompletedTask;
    public Task DestroyAsync   (CancellationToken ct = default) => Task.CompletedTask;
}
```

### `PluginException`

Typed `FireflyException` with stable error code `PLUGIN_ERROR`.

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
