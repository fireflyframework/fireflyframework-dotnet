// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Admin.Server;
using FluentAssertions;
using Xunit;

namespace FireflyFramework.Tests;

public sealed class AdminTests
{
    [Fact]
    public void Register_then_heartbeat_advances_status_and_timestamp()
    {
        var registry = new InMemoryInstanceRegistry();
        var instance = new AdminInstance("id-1", "orders", "http://m", "http://h", DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddMinutes(-5), "DOWN", new Dictionary<string, string>());

        var stored = registry.Register(instance);
        stored.Status.Should().Be("DOWN");

        var heartbeat = registry.Heartbeat("id-1", "UP");
        heartbeat.Should().NotBeNull();
        heartbeat!.Status.Should().Be("UP");
        heartbeat.LastHeartbeat.Should().BeOnOrAfter(stored.LastHeartbeat);
    }

    [Fact]
    public void Deregister_removes_instance()
    {
        var registry = new InMemoryInstanceRegistry();
        registry.Register(new AdminInstance("id-2", "x", "", "", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "UP", new Dictionary<string, string>()));

        registry.Deregister("id-2").Should().BeTrue();
        registry.Get("id-2").Should().BeNull();
        registry.Deregister("id-2").Should().BeFalse();
    }

    [Fact]
    public void EvictStale_keeps_fresh_instances()
    {
        // Deterministic: register two instances and immediately evict with a generous
        // timeout — neither should be dropped because both heartbeats are fresh.
        var registry = new InMemoryInstanceRegistry();
        registry.Register(new AdminInstance("a", "x", "", "", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "UP", new Dictionary<string, string>()));
        registry.Register(new AdminInstance("b", "x", "", "", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "UP", new Dictionary<string, string>()));

        registry.EvictStale(TimeSpan.FromMinutes(5));

        registry.All().Should().HaveCount(2);
    }

    [Fact]
    public async Task EvictStale_drops_instances_past_timeout()
    {
        // Deterministic: register, sleep beyond the eviction window, then evict.
        var registry = new InMemoryInstanceRegistry();
        registry.Register(new AdminInstance("stale", "x", "", "", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "UP", new Dictionary<string, string>()));

        await Task.Delay(50);
        registry.EvictStale(TimeSpan.FromMilliseconds(1));

        registry.All().Should().BeEmpty();
    }
}
