// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.WebSocket.Core;
using FluentAssertions;
using Xunit;

namespace FireflyFramework.Tests;

public sealed class WebSocketRegistryTests
{
    [Fact]
    public async Task Broadcast_targets_only_listed_group()
    {
        var registry = new WebSocketSessionRegistry();
        var alpha = new FakeSession("A");
        var beta = new FakeSession("B");

        registry.Add(alpha, "alpha");
        registry.Add(beta, "beta");

        await registry.BroadcastAsync("hello-alpha", "alpha");

        alpha.Sent.Should().Contain("hello-alpha");
        beta.Sent.Should().BeEmpty();
    }

    [Fact]
    public void Remove_drops_session_from_groups()
    {
        var registry = new WebSocketSessionRegistry();
        var s = new FakeSession("X");
        registry.Add(s, "g1", "g2");
        registry.InGroup("g1").Should().HaveCount(1);

        registry.Remove("X");
        registry.InGroup("g1").Should().BeEmpty();
        registry.InGroup("g2").Should().BeEmpty();
        registry.All().Should().BeEmpty();
    }

    private sealed class FakeSession : IWebSocketSession
    {
        public FakeSession(string id) { Id = id; }
        public string Id { get; }
        public string Path => "/";
        public IReadOnlyDictionary<string, string?> Headers { get; } = new Dictionary<string, string?>();
        public string? SubProtocol => null;
        public bool IsOpen => true;
        public List<string> Sent { get; } = new();

        public Task SendTextAsync(string payload, CancellationToken ct) { Sent.Add(payload); return Task.CompletedTask; }
        public Task SendBinaryAsync(ReadOnlyMemory<byte> payload, CancellationToken ct) => Task.CompletedTask;
        public Task CloseAsync(string? reason = null, CancellationToken ct = default) => Task.CompletedTask;
    }
}
