// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Session.Adapters;
using FireflyFramework.Session.Core;
using FluentAssertions;
using Xunit;

namespace FireflyFramework.Tests;

public sealed class SessionTests
{
    [Fact]
    public async Task InMemoryStore_round_trips_session()
    {
        var store = new InMemorySessionStore();
        var session = await store.CreateAsync(TimeSpan.FromMinutes(10), CancellationToken.None);
        session.Set("user", "alice");
        await store.SaveAsync(session, CancellationToken.None);

        var loaded = await store.LoadAsync(session.Id, CancellationToken.None);
        loaded.Should().NotBeNull();
        loaded!.TryGet<string>("user", out var user).Should().BeTrue();
        user.Should().Be("alice");
    }

    [Fact]
    public async Task Expired_session_is_evicted_on_load()
    {
        var store = new InMemorySessionStore();
        var session = await store.CreateAsync(TimeSpan.FromMilliseconds(20), CancellationToken.None);
        await store.SaveAsync(session, CancellationToken.None);

        await Task.Delay(80);

        var loaded = await store.LoadAsync(session.Id, CancellationToken.None);
        loaded.Should().BeNull();
    }

    [Fact]
    public void FireflySession_keys_snapshot_reflects_attributes()
    {
        var s = new FireflySession("id", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5));
        s.Set("a", 1);
        s.Set("b", "two");
        s.Keys.Should().BeEquivalentTo(new[] { "a", "b" });
        s.Snapshot()["b"].Should().Be("two");
        s.Remove("a");
        s.Keys.Should().BeEquivalentTo(new[] { "b" });
    }
}
