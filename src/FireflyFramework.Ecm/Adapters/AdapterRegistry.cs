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

namespace FireflyFramework.Ecm.Adapters;

/// <summary>
/// Runtime registry of ECM adapters discovered from the DI container or scanned via
/// reflection. Mirrors Java <c>AdapterRegistry</c>.
/// </summary>
public interface IAdapterRegistry
{
    /// <summary>Registers an adapter instance under its declared <see cref="EcmAdapterAttribute"/> type.</summary>
    void Register(object adapter);

    AdapterInfo? GetInfo(string type);

    IReadOnlyList<AdapterInfo> All();

    IReadOnlyList<AdapterInfo> SupportingFeature(AdapterFeature feature);

    IReadOnlyList<T> Resolve<T>() where T : class;

    T? ResolveByType<T>(string type) where T : class;
}

public sealed class AdapterRegistry : IAdapterRegistry
{
    private readonly ConcurrentDictionary<string, (AdapterInfo Info, object Instance)> _byType = new(StringComparer.OrdinalIgnoreCase);

    public void Register(object adapter)
    {
        var info = AdapterIntrospection.GetInfo(adapter.GetType())
            ?? throw new InvalidOperationException($"{adapter.GetType().FullName} is not annotated with [EcmAdapter].");
        _byType[info.Type] = (info, adapter);
    }

    public AdapterInfo? GetInfo(string type) =>
        _byType.TryGetValue(type, out var entry) ? entry.Info : null;

    public IReadOnlyList<AdapterInfo> All() =>
        _byType.Values.Select(e => e.Info).ToList();

    public IReadOnlyList<AdapterInfo> SupportingFeature(AdapterFeature feature) =>
        _byType.Values
            .Where(e => e.Info.Enabled && e.Info.SupportedFeatures.HasFlag(feature))
            .Select(e => e.Info)
            .ToList();

    public IReadOnlyList<T> Resolve<T>() where T : class =>
        _byType.Values
            .Where(e => e.Info.Enabled && e.Instance is T)
            .Select(e => (T)e.Instance)
            .ToList();

    public T? ResolveByType<T>(string type) where T : class =>
        _byType.TryGetValue(type, out var entry) && entry.Info.Enabled
            ? entry.Instance as T
            : null;
}

/// <summary>
/// Helper that extracts <see cref="AdapterInfo"/> from a class decorated with
/// <see cref="EcmAdapterAttribute"/>. Mirrors Java reflection logic in
/// <c>AdapterRegistry.processAdapter</c>.
/// </summary>
public static class AdapterIntrospection
{
    public static AdapterInfo? GetInfo(Type implementationType)
    {
        var attr = implementationType.GetCustomAttributes(typeof(EcmAdapterAttribute), inherit: false)
            .Cast<EcmAdapterAttribute>()
            .FirstOrDefault();
        if (attr is null) return null;

        return new AdapterInfo(
            attr.Type,
            attr.Description ?? string.Empty,
            attr.Priority,
            attr.Enabled,
            attr.SupportedFeatures,
            attr.RequiredProperties,
            attr.OptionalProperties,
            implementationType);
    }
}
