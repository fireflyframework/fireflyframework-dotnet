using FireflyFramework.Eda.Events;
using Google.Protobuf;

namespace FireflyFramework.Eda.Serialization;

/// <summary>Protobuf binary serializer. Mirrors Java <c>ProtobufMessageSerializer</c>.</summary>
public sealed class ProtobufMessageSerializer : IMessageSerializer
{
    public SerializationFormat Format => SerializationFormat.Protobuf;

    public byte[] Serialize<T>(T value)
    {
        if (value is not IMessage message)
        {
            throw new InvalidOperationException(
                $"Protobuf serialization requires types implementing IMessage; got {typeof(T).Name}.");
        }

        return message.ToByteArray();
    }

    public T? Deserialize<T>(byte[] data)
    {
        var instance = Activator.CreateInstance<T>() as IMessage
            ?? throw new InvalidOperationException(
                $"Protobuf deserialization requires types implementing IMessage; got {typeof(T).Name}.");

        instance.MergeFrom(data);
        return (T?)instance;
    }
}
