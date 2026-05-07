using FireflyFramework.Eda.Events;

namespace FireflyFramework.Eda.Serialization;

public interface IMessageSerializer
{
    SerializationFormat Format { get; }
    byte[] Serialize<T>(T value);
    T? Deserialize<T>(byte[] data);
}
