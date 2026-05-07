using System.Text.Json;
using FireflyFramework.Eda.Events;

namespace FireflyFramework.Eda.Serialization;

public sealed class JsonMessageSerializer : IMessageSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public SerializationFormat Format => SerializationFormat.Json;

    public byte[] Serialize<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Options);

    public T? Deserialize<T>(byte[] data) => JsonSerializer.Deserialize<T>(data, Options);
}
