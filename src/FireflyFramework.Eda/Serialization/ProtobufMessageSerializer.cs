// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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
