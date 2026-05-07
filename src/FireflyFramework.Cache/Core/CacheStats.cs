namespace FireflyFramework.Cache.Core;

/// <summary>Cache statistics. Mirrors Java <c>CacheStats</c>.</summary>
public sealed record CacheStats(
    CacheType Type,
    string Name,
    long RequestCount,
    long HitCount,
    long MissCount,
    long LoadCount,
    long EvictionCount,
    long EntryCount,
    TimeSpan AverageLoadTime,
    long EstimatedSizeBytes,
    DateTimeOffset CapturedAt)
{
    public double HitRate => RequestCount == 0 ? 0 : (double)HitCount / RequestCount;
    public double MissRate => RequestCount == 0 ? 0 : (double)MissCount / RequestCount;

    public static CacheStats Empty(CacheType type, string name) => new(
        type, name, 0, 0, 0, 0, 0, 0, TimeSpan.Zero, 0, DateTimeOffset.UtcNow);
}
