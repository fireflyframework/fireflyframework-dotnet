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

using FireflyFramework.Eda.Serialization;
using FluentAssertions;
using Xunit;

namespace FireflyFramework.Tests;

public class SerializerTests
{
    public sealed record OrderPlaced(Guid OrderId, decimal Total, string Currency);

    [Fact]
    public void JsonSerializer_round_trips_record()
    {
        var ser = new JsonMessageSerializer();
        var original = new OrderPlaced(Guid.NewGuid(), 199.99m, "USD");
        var bytes = ser.Serialize(original);
        var parsed = ser.Deserialize<OrderPlaced>(bytes);
        parsed.Should().NotBeNull();
        parsed!.OrderId.Should().Be(original.OrderId);
        parsed.Total.Should().Be(199.99m);
        parsed.Currency.Should().Be("USD");
    }

    [Fact]
    public void ProtobufSerializer_rejects_non_protobuf_types()
    {
        var ser = new ProtobufMessageSerializer();
        Action act = () => ser.Serialize(new OrderPlaced(Guid.NewGuid(), 1m, "USD"));
        act.Should().Throw<InvalidOperationException>().WithMessage("*IMessage*");
    }

    [Fact]
    public void AvroSerializer_rejects_non_avro_types()
    {
        var ser = new AvroMessageSerializer();
        Action act = () => ser.Serialize(new OrderPlaced(Guid.NewGuid(), 1m, "USD"));
        act.Should().Throw<InvalidOperationException>().WithMessage("*ISpecificRecord*");
    }
}
