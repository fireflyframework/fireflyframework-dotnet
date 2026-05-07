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

[Plugin("greeter", "Greeter Plugin", "1.0.0")]
public sealed class GreeterPlugin : IPlugin
{
    public bool Initialized { get; private set; }
    public bool Started { get; private set; }
    public bool Stopped { get; private set; }

    public PluginMetadata Metadata => new("greeter", "Greeter Plugin", "1.0.0", null, null, Array.Empty<string>());

    public Task InitializeAsync(CancellationToken ct = default) { Initialized = true; return Task.CompletedTask; }
    public Task StartAsync(CancellationToken ct = default) { Started = true; return Task.CompletedTask; }
    public Task StopAsync(CancellationToken ct = default) { Stopped = true; return Task.CompletedTask; }
    public Task DestroyAsync(CancellationToken ct = default) => Task.CompletedTask;
}

public class PluginsTests
{
    [Fact]
    public async Task Plugin_lifecycle_runs_init_then_start_then_stop()
    {
        var registry = new DefaultExtensionRegistry();
        var manager = new DefaultPluginManager(NullLogger<DefaultPluginManager>.Instance, registry);

        var descriptor = await manager.LoadAsync(typeof(GreeterPlugin));
        descriptor.State.Should().Be(PluginState.Initialized);

        await manager.StartAsync("greeter");
        manager.GetDescriptor("greeter")!.State.Should().Be(PluginState.Started);

        var plugin = (GreeterPlugin)manager.GetPlugin("greeter")!;
        plugin.Initialized.Should().BeTrue();
        plugin.Started.Should().BeTrue();

        await manager.StopAsync("greeter");
        manager.GetDescriptor("greeter")!.State.Should().Be(PluginState.Stopped);
        plugin.Stopped.Should().BeTrue();
    }

    [Fact]
    public void ExtensionRegistry_returns_extensions_by_priority()
    {
        var reg = new DefaultExtensionRegistry();
        reg.RegisterExtensionPoint("greeting", typeof(Func<string>));
        reg.RegisterExtension<Func<string>>("greeting", () => "low", priority: 1);
        reg.RegisterExtension<Func<string>>("greeting", () => "high", priority: 10);
        var top = reg.GetExtension<Func<string>>("greeting")!;
        top().Should().Be("high");
        reg.GetExtensions<Func<string>>("greeting").Should().HaveCount(2);
    }
}
