namespace FireflyFramework.Plugins.Api;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class PluginAttribute : Attribute
{
    public PluginAttribute(string id, string name, string version)
    {
        Id = id; Name = name; Version = version;
    }

    public string Id { get; }
    public string Name { get; }
    public string Version { get; }
    public string? Description { get; set; }
    public string? Author { get; set; }
    public string[] Dependencies { get; set; } = Array.Empty<string>();
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ExtensionAttribute : Attribute
{
    public ExtensionAttribute(string pointId) => PointId = pointId;
    public string PointId { get; }
    public int Priority { get; set; }
}

[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
public sealed class ExtensionPointAttribute : Attribute
{
    public ExtensionPointAttribute(string id) => Id = id;
    public string Id { get; }
}
