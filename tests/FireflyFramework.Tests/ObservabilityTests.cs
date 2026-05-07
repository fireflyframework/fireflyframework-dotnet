using FireflyFramework.Observability.Metrics;
using FluentAssertions;
using Xunit;

namespace FireflyFramework.Tests;

public class ObservabilityTests
{
    [Theory]
    [InlineData("cqrs", "firefly.cqrs")]
    [InlineData("eda", "firefly.eda")]
    [InlineData("event_sourcing", "firefly.event_sourcing")]
    public void MetricNaming_prefix_format(string module, string expected)
    {
        MetricNaming.Prefix(module).Should().Be(expected);
    }

    [Theory]
    [InlineData("UPPER")]
    [InlineData("with-dash")]
    [InlineData("contains.dot")]
    public void MetricNaming_rejects_invalid_module_names(string module)
    {
        Action act = () => MetricNaming.Prefix(module);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MetricNaming_combines_prefix_and_name()
    {
        MetricNaming.Name("firefly.cqrs", "command.duration_ms").Should().Be("firefly.cqrs.command.duration_ms");
    }
}
