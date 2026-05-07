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
