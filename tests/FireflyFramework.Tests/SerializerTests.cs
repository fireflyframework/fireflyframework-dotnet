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
