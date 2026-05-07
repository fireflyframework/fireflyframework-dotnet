using FireflyFramework.Kernel.Exceptions;
using FluentAssertions;
using Xunit;

namespace FireflyFramework.Tests;

public class KernelTests
{
    [Fact]
    public void FireflyException_carries_error_code_and_context()
    {
        var ex = new FireflyException("boom", "WIDGET_BROKEN", new Dictionary<string, object?> { ["widget"] = "alpha" }, null);
        ex.ErrorCode.Should().Be("WIDGET_BROKEN");
        ex.Context.Should().ContainKey("widget").WhoseValue.Should().Be("alpha");
    }

    [Fact]
    public void FireflyInfrastructureException_keeps_default_code()
    {
        var ex = new FireflyInfrastructureException("db down");
        ex.ErrorCode.Should().Be("FIREFLY_INFRASTRUCTURE_ERROR");
    }

    [Fact]
    public void FireflySecurityException_keeps_default_code()
    {
        var ex = new FireflySecurityException("forbidden");
        ex.ErrorCode.Should().Be("FIREFLY_SECURITY_ERROR");
    }
}
