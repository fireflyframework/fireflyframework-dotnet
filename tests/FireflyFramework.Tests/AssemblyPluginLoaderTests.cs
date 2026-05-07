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
