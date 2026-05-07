using FireflyFramework.Kernel.Exceptions;

namespace FireflyFramework.Plugins.Api;

public sealed record PluginMetadata(
    string Id,
    string Name,
    string Version,
    string? Description,
    string? Author,
    IReadOnlyList<string> Dependencies);

public enum PluginState { Loaded, Initialized, Started, Stopped, Destroyed }

public sealed record PluginDescriptor(PluginMetadata Metadata, PluginState State, DateTimeOffset LoadedAt, DateTimeOffset LastStateChange)
{
    public string Id => Metadata.Id;
    public string Name => Metadata.Name;
    public string Version => Metadata.Version;
    public PluginDescriptor WithState(PluginState s) => this with { State = s, LastStateChange = DateTimeOffset.UtcNow };
}

public class PluginException : FireflyException
{
    public PluginException(string message) : base(message, "PLUGIN_ERROR") { }
    public PluginException(string message, Exception cause) : base(message, "PLUGIN_ERROR", cause) { }
}

/// <summary>Plugin lifecycle contract. Mirrors Java <c>Plugin</c>.</summary>
public interface IPlugin
{
    PluginMetadata Metadata { get; }
    Task InitializeAsync(CancellationToken ct = default);
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    Task DestroyAsync(CancellationToken ct = default);
}

/// <summary>High-level plugin lifecycle management. Mirrors Java <c>PluginManager</c>.</summary>
public interface IPluginManager
{
    Task<PluginDescriptor> LoadAsync(Type pluginType, CancellationToken ct = default);
    Task StartAsync(string pluginId, CancellationToken ct = default);
    Task StopAsync(string pluginId, CancellationToken ct = default);
    Task UnloadAsync(string pluginId, CancellationToken ct = default);
    IPlugin? GetPlugin(string pluginId);
    PluginDescriptor? GetDescriptor(string pluginId);
    IReadOnlyList<PluginDescriptor> All();
    IExtensionRegistry Extensions { get; }
}

/// <summary>Extension point registry. Mirrors Java <c>ExtensionRegistry</c>.</summary>
public interface IExtensionRegistry
{
    void RegisterExtensionPoint(string id, Type contract);
    void RegisterExtension<T>(string pointId, T extension, int priority = 0);
    void UnregisterExtension<T>(string pointId, T extension);
    IReadOnlyList<T> GetExtensions<T>(string pointId);
    T? GetExtension<T>(string pointId);
    IReadOnlyList<string> GetExtensionPointIds();
    bool HasExtensionPoint(string pointId);
}
