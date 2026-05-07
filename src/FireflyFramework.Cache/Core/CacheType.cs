namespace FireflyFramework.Cache.Core;

/// <summary>Cache backend kind. Mirrors Java <c>CacheType</c>.</summary>
public enum CacheType
{
    Memory,
    Redis,
    Hazelcast,
    JCache,
    NoOp,
    Auto,
}

public static class CacheTypeExtensions
{
    public static bool IsDistributed(this CacheType type) =>
        type is CacheType.Redis or CacheType.Hazelcast or CacheType.JCache;

    public static bool SupportsPersistence(this CacheType type) =>
        type is CacheType.Redis or CacheType.Hazelcast;

    public static string DisplayName(this CacheType type) => type switch
    {
        CacheType.Memory => "Memory",
        CacheType.Redis => "Redis",
        CacheType.Hazelcast => "Hazelcast",
        CacheType.JCache => "JCache",
        CacheType.NoOp => "NoOp",
        CacheType.Auto => "Auto",
        _ => type.ToString(),
    };
}
