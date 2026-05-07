using System.Text.Json;
using FireflyFramework.Cache.Core;

namespace FireflyFramework.Cache.Serialization;

public sealed class JsonCacheSerializer : ICacheSerializer
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public string Format => "application/json";

    public byte[] Serialize<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Options);

    public T? Deserialize<T>(byte[] data) => JsonSerializer.Deserialize<T>(data, Options);
}
