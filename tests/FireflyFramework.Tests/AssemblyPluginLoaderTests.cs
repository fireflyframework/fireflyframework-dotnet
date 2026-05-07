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

using FireflyFramework.Plugins.Api;
using FireflyFramework.Plugins.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FireflyFramework.Tests;

public class AssemblyPluginLoaderTests
{
    [Fact]
    public async Task LoadFromAssembly_throws_when_path_does_not_exist()
    {
        var registry = new DefaultExtensionRegistry();
        var manager = new DefaultPluginManager(NullLogger<DefaultPluginManager>.Instance, registry);
        await using var loader = new AssemblyPluginLoader(manager, NullLogger<AssemblyPluginLoader>.Instance);

        await FluentActions.Invoking(() => loader.LoadFromAssemblyAsync("/does/not/exist.dll"))
            .Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task LoadFromAssembly_loads_plugin_from_current_test_assembly()
    {
        var registry = new DefaultExtensionRegistry();
        var manager = new DefaultPluginManager(NullLogger<DefaultPluginManager>.Instance, registry);
        await using var loader = new AssemblyPluginLoader(manager, NullLogger<AssemblyPluginLoader>.Instance);

        // The test assembly itself contains GreeterPlugin (defined in PluginsTests.cs)
        var path = typeof(GreeterPlugin).Assembly.Location;
        var descriptors = await loader.LoadFromAssemblyAsync(path);

        descriptors.Should().NotBeEmpty();
        descriptors.Should().Contain(d => d.Id == "greeter");
    }

    [Fact]
    public async Task UnloadAsync_removes_plugin_from_manager()
    {
        var registry = new DefaultExtensionRegistry();
        var manager = new DefaultPluginManager(NullLogger<DefaultPluginManager>.Instance, registry);
        await using var loader = new AssemblyPluginLoader(manager, NullLogger<AssemblyPluginLoader>.Instance);

        var path = typeof(GreeterPlugin).Assembly.Location;
        var descriptors = await loader.LoadFromAssemblyAsync(path);
        var greeter = descriptors.First(d => d.Id == "greeter");

        await loader.UnloadAsync(greeter.Id);

        manager.GetDescriptor(greeter.Id).Should().BeNull();
    }
}
