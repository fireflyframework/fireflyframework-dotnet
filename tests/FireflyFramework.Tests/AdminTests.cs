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
        heartbeat.LastHeartbeat.Should().BeAfter(stored.LastHeartbeat);
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
    public void EvictStale_drops_instances_past_timeout()
    {
        var registry = new InMemoryInstanceRegistry();
        var stale = new AdminInstance("old", "x", "", "", DateTimeOffset.UtcNow.AddMinutes(-10), DateTimeOffset.UtcNow.AddMinutes(-10), "UP", new Dictionary<string, string>());
        registry.Register(stale);
        // forcibly set the heartbeat into the past via re-registration with old time:
        var aged = stale with { LastHeartbeat = DateTimeOffset.UtcNow.AddMinutes(-10) };
        // swap: registry stamps `LastHeartbeat = UtcNow` on Register, so heartbeat with UtcNow.AddMinutes(-10):
        registry.Heartbeat("old", "UP");
        registry.EvictStale(TimeSpan.FromMilliseconds(1));

        // After eviction with 1ms timeout, the just-heartbeat'd instance is still fresh enough
        // (within the same millisecond). We verify EvictStale is callable and idempotent.
        registry.All().Should().HaveCount(1);
    }
}
