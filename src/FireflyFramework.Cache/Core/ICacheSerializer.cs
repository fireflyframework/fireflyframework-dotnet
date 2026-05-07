namespace FireflyFramework.Cache.Core;

/// <summary>Serializer SPI for distributed caches. Mirrors Java <c>CacheSerializer</c>.</summary>
public interface ICacheSerializer
{
    byte[] Serialize<T>(T value);
    T? Deserialize<T>(byte[] data);
    string Format { get; }
}
