using FireflyFramework.Web.Logging;
using Xunit;

namespace FireflyFramework.Tests;

public class FireflyBannerTests
{
    private const char Esc = '';

    public FireflyBannerTests()
    {
        FireflyBanner.ResetForTests();
    }

    [Theory]
    [InlineData(typeof(FireflyFramework.Starter.Core.FireflyCoreExtensions), "firefly-core")]
    [InlineData(typeof(FireflyFramework.Starter.Application.FireflyApplicationExtensions), "firefly-application")]
    [InlineData(typeof(FireflyFramework.Starter.Domain.FireflyDomainExtensions), "firefly-domain")]
    [InlineData(typeof(FireflyFramework.Starter.Data.FireflyDataExtensions), "firefly-data")]
    public void EachStarter_EmbedsItsOwnBanner(Type extensionsType, string expectedTag)
    {
        var rendered = FireflyBanner.RenderForTests(extensionsType.Assembly, "orders-service", "1.2.3", ansi: false);
        Assert.NotNull(rendered);
        Assert.Contains($":: {expectedTag} ::", rendered);
    }

    [Fact]
    public void Render_SubstitutesApplicationNameAndVersion()
    {
        var rendered = FireflyBanner.RenderForTests(
            typeof(FireflyFramework.Starter.Core.FireflyCoreExtensions).Assembly,
            "orders-service", "9.9.9", ansi: false)!;

        Assert.Contains("orders-service", rendered);
        Assert.Contains("9.9.9", rendered);
        Assert.Contains("Logs start below", rendered);
    }

    [Fact]
    public void Render_AnsiOff_StripsColorCodes()
    {
        var rendered = FireflyBanner.RenderForTests(
            typeof(FireflyFramework.Starter.Core.FireflyCoreExtensions).Assembly,
            "orders-service", "1.0.0", ansi: false)!;

        Assert.DoesNotContain(Esc, rendered);
        Assert.DoesNotContain("${AnsiColor", rendered);
        Assert.DoesNotContain("${AnsiStyle", rendered);
    }

    [Fact]
    public void Render_AnsiOn_EmitsEscapeCodes()
    {
        var rendered = FireflyBanner.RenderForTests(
            typeof(FireflyFramework.Starter.Core.FireflyCoreExtensions).Assembly,
            "orders-service", "1.0.0", ansi: true)!;

        Assert.Contains($"{Esc}[31m", rendered);   // red
        Assert.Contains($"{Esc}[32m", rendered);   // green
        Assert.Contains($"{Esc}[33m", rendered);   // yellow
        Assert.Contains($"{Esc}[1m", rendered);    // bold
        Assert.Contains($"{Esc}[22m", rendered);   // normal
        Assert.DoesNotContain("${AnsiColor", rendered);
    }

    [Fact]
    public void Render_IncludesDotnetRuntimeDescription()
    {
        var rendered = FireflyBanner.RenderForTests(
            typeof(FireflyFramework.Starter.Core.FireflyCoreExtensions).Assembly,
            "orders-service", "1.0.0", ansi: false)!;

        Assert.Contains(".NET", rendered);
    }

    [Fact]
    public void Print_OnlyEmitsOnce()
    {
        var first = new StringWriter();
        FireflyBanner.Print(typeof(FireflyFramework.Starter.Core.FireflyCoreExtensions).Assembly,
            "svc", "1.0.0", first);

        var second = new StringWriter();
        FireflyBanner.Print(typeof(FireflyFramework.Starter.Application.FireflyApplicationExtensions).Assembly,
            "svc", "1.0.0", second);

        Assert.NotEmpty(first.ToString());
        Assert.Empty(second.ToString());
    }
}
