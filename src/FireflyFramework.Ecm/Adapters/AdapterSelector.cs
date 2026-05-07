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

namespace FireflyFramework.Ecm.Adapters;

/// <summary>
/// Picks an adapter implementing port <typeparamref name="TPort"/> by feature, type or
/// configured preference. Mirrors Java <c>AdapterSelector</c>.
/// </summary>
public sealed class AdapterSelector<TPort> where TPort : class
{
    private readonly IAdapterRegistry _registry;

    public AdapterSelector(IAdapterRegistry registry) => _registry = registry;

    /// <summary>Returns the highest-priority adapter that supports the requested feature.</summary>
    public TPort? PickByFeature(AdapterFeature feature) =>
        _registry.SupportingFeature(feature)
            .Where(i => typeof(TPort).IsAssignableFrom(i.ImplementationType))
            .OrderByDescending(i => i.Priority)
            .Select(i => _registry.ResolveByType<TPort>(i.Type))
            .FirstOrDefault(adapter => adapter is not null);

    /// <summary>Returns the adapter for an explicit type id, if registered.</summary>
    public TPort? PickByType(string type) => _registry.ResolveByType<TPort>(type);

    /// <summary>Returns every registered adapter that implements <typeparamref name="TPort"/>.</summary>
    public IReadOnlyList<TPort> All() => _registry.Resolve<TPort>();
}
