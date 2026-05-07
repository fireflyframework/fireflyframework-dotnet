using FireflyFramework.Cache.Core;

namespace FireflyFramework.Cache.Configuration;

/// <summary>Configuration root for the cache module. Mirrors Java <c>CacheProperties</c>.</summary>
public sealed class FireflyCacheOptions
{
    public const string SectionName = "Firefly:Cache";

    public CacheType Provider { get; set; } = CacheType.Auto;
    public string Name { get; set; } = "default";
    public string KeyPrefix { get; set; } = "firefly:cache:";
    public RedisCacheOptions Redis { get; set; } = new();
    public MemoryCacheOptions Memory { get; set; } = new();
}

public sealed class RedisCacheOptions
{
    public string ConnectionString { get; set; } = "localhost:6379";
    public TimeSpan? DefaultTtl { get; set; }
}

public sealed class MemoryCacheOptions
{
    public long? SizeLimit { get; set; }
}
