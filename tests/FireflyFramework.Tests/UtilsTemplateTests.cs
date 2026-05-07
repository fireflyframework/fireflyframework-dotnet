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
