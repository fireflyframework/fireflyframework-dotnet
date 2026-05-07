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
