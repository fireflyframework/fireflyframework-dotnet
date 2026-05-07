// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Messaging.Adapters;
using FireflyFramework.Messaging.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FireflyFramework.Tests;

public sealed class MessagingTests
{
    public sealed record OrderShipped(string OrderId, DateTime At);

    [Fact]
    public async Task InMemoryBroker_delivers_to_subscribers()
    {
        var broker = new InMemoryMessageBroker(NullLogger<InMemoryMessageBroker>.Instance);
        OrderShipped? received = null;

        using var sub = broker.Subscribe<OrderShipped>("orders.shipped", (m, _) =>
        {
            received = m.Payload;
            return Task.CompletedTask;
        });

        await broker.SendAsync("orders.shipped", Message<OrderShipped>.Of(new OrderShipped("o-1", DateTime.UtcNow)));
        received.Should().NotBeNull();
        received!.OrderId.Should().Be("o-1");
    }

    [Fact]
    public async Task Disposing_subscription_stops_delivery()
    {
        var broker = new InMemoryMessageBroker(NullLogger<InMemoryMessageBroker>.Instance);
        var count = 0;
        var sub = broker.Subscribe<OrderShipped>("orders.shipped", (_, _) => { count++; return Task.CompletedTask; });

        await broker.SendAsync("orders.shipped", Message<OrderShipped>.Of(new OrderShipped("a", DateTime.UtcNow)));
        sub.Dispose();
        await broker.SendAsync("orders.shipped", Message<OrderShipped>.Of(new OrderShipped("b", DateTime.UtcNow)));

        count.Should().Be(1);
    }

    [Fact]
    public void Message_WithHeader_returns_new_envelope()
    {
        var msg = Message<string>.Of("hi");
        var withTrace = msg.WithHeader("x-trace-id", "abc");
        msg.GetHeader("x-trace-id").Should().BeNull();
        withTrace.GetHeader("x-trace-id").Should().Be("abc");
    }
}
