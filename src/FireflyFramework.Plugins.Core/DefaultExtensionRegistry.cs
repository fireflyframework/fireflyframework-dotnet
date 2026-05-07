using System.Collections.Concurrent;
using FireflyFramework.Plugins.Api;

namespace FireflyFramework.Plugins.Core;

public sealed class DefaultExtensionRegistry : IExtensionRegistry
{
    private readonly ConcurrentDictionary<string, Type> _points = new();
    private readonly ConcurrentDictionary<string, List<(object Extension, int Priority)>> _extensions = new();

    public void RegisterExtensionPoint(string id, Type contract) => _points[id] = contract;

    public void RegisterExtension<T>(string pointId, T extension, int priority = 0)
    {
        if (extension is null) throw new ArgumentNullException(nameof(extension));
        var list = _extensions.GetOrAdd(pointId, _ => new List<(object, int)>());
        lock (list)
        {
            list.Add((extension, priority));
            list.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
    }

    public void UnregisterExtension<T>(string pointId, T extension)
    {
        if (extension is null) return;
        if (_extensions.TryGetValue(pointId, out var list))
        {
            lock (list)
            {
                list.RemoveAll(t => ReferenceEquals(t.Extension, extension));
            }
        }
    }

    public IReadOnlyList<T> GetExtensions<T>(string pointId)
    {
        if (!_extensions.TryGetValue(pointId, out var list)) return Array.Empty<T>();
        lock (list)
        {
            return list.Select(t => t.Extension).OfType<T>().ToList();
        }
    }

    public T? GetExtension<T>(string pointId) => GetExtensions<T>(pointId).FirstOrDefault();

    public IReadOnlyList<string> GetExtensionPointIds() => _points.Keys.ToList();

    public bool HasExtensionPoint(string pointId) => _points.ContainsKey(pointId);
}
