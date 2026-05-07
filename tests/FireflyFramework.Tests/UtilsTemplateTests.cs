using FireflyFramework.Utils.Templates;
using FluentAssertions;
using Xunit;

namespace FireflyFramework.Tests;

public class UtilsTemplateTests
{
    [Fact]
    public void TemplateRenderUtil_renders_string_template()
    {
        var html = TemplateRenderUtil.RenderTemplateStringToHtml(
            "Hello {{ name }}!",
            "greeting",
            new Dictionary<string, object?> { ["name"] = "World" });
        html.Should().Be("Hello World!");
    }

    [Fact]
    public void Shared_variables_apply_across_renders()
    {
        TemplateRenderUtil.AddSharedVariable("brand", "Firefly");
        try
        {
            var html = TemplateRenderUtil.RenderTemplateStringToHtml(
                "{{ brand }} says hi to {{ name }}",
                "shared", new Dictionary<string, object?> { ["name"] = "you" });
            html.Should().Be("Firefly says hi to you");
        }
        finally
        {
            TemplateRenderUtil.ClearSharedVariables();
        }
    }

    [Fact]
    public void Validate_returns_no_errors_for_valid_template()
    {
        TemplateRenderUtil.ValidateTemplate("hello {{ name }}").Should().BeEmpty();
    }
}
