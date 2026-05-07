using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using FireflyFramework.Eda.Events;

namespace FireflyFramework.Eda.Serialization;

/// <summary>
/// Schema-Registry-aware Avro serializer. Wraps Confluent's
/// <see cref="AvroSerializer{T}"/> + <see cref="AvroDeserializer{T}"/> behind the
/// Firefly <see cref="IMessageSerializer"/> contract so EDA publishers and consumers
/// can negotiate schemas with a Confluent Schema Registry.
/// </summary>
public sealed class SchemaRegistryAvroSerializer<T> : IMessageSerializer where T : class
{
    private readonly AvroSerializer<T> _serializer;
    private readonly AvroDeserializer<T> _deserializer;

    public SchemaRegistryAvroSerializer(ISchemaRegistryClient client)
    {
        _serializer = new AvroSerializer<T>(client);
        _deserializer = new AvroDeserializer<T>(client);
    }

    public SerializationFormat Format => SerializationFormat.Avro;

    public byte[] Serialize<TInput>(TInput value)
    {
        if (value is not T typed)
        {
            throw new InvalidOperationException(
                $"Schema-Registry Avro serializer is bound to {typeof(T).Name}; received {typeof(TInput).Name}.");
        }

        var ctx = new Confluent.Kafka.SerializationContext(Confluent.Kafka.MessageComponentType.Value, "firefly");
        return _serializer.SerializeAsync(typed, ctx).GetAwaiter().GetResult();
    }

    public TOutput? Deserialize<TOutput>(byte[] data)
    {
        var ctx = new Confluent.Kafka.SerializationContext(Confluent.Kafka.MessageComponentType.Value, "firefly");
        var typed = _deserializer.DeserializeAsync(data, isNull: data is null, ctx).GetAwaiter().GetResult();
        return typed is TOutput cast ? cast : default;
    }
}

/// <summary>
/// Schema-Registry-aware Protobuf serializer. Wraps Confluent's
/// <see cref="ProtobufSerializer{T}"/> + <see cref="ProtobufDeserializer{T}"/>.
/// </summary>
public sealed class SchemaRegistryProtobufSerializer<T> : IMessageSerializer
    where T : class, Google.Protobuf.IMessage<T>, new()
{
    private readonly ProtobufSerializer<T> _serializer;
    private readonly ProtobufDeserializer<T> _deserializer;

    public SchemaRegistryProtobufSerializer(ISchemaRegistryClient client)
    {
        _serializer = new ProtobufSerializer<T>(client);
        _deserializer = new ProtobufDeserializer<T>();
    }

    public SerializationFormat Format => SerializationFormat.Protobuf;

    public byte[] Serialize<TInput>(TInput value)
    {
        if (value is not T typed)
        {
            throw new InvalidOperationException(
                $"Schema-Registry Protobuf serializer is bound to {typeof(T).Name}; received {typeof(TInput).Name}.");
        }

        var ctx = new Confluent.Kafka.SerializationContext(Confluent.Kafka.MessageComponentType.Value, "firefly");
        return _serializer.SerializeAsync(typed, ctx).GetAwaiter().GetResult();
    }

    public TOutput? Deserialize<TOutput>(byte[] data)
    {
        var ctx = new Confluent.Kafka.SerializationContext(Confluent.Kafka.MessageComponentType.Value, "firefly");
        var typed = _deserializer.DeserializeAsync(data, isNull: data is null, ctx).GetAwaiter().GetResult();
        return typed is TOutput cast ? cast : default;
    }
}
