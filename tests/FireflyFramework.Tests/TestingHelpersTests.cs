// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

using FireflyFramework.Eda.Events;
using FireflyFramework.Testing.Assertions;
using FireflyFramework.Testing.Eda;
using FluentAssertions;
using Xunit;

namespace FireflyFramework.Tests;

public sealed class TestingHelpersTests
{
    public sealed record OrderCreated(string Id);
    public sealed record OrderShipped(string Id);

    [Fact]
    public async Task EventCapturePublisher_records_published_envelopes()
    {
        var publisher = new EventCapturePublisher();
        await publisher.PublishAsync(new EventEnvelope("orders", "OrderCreated", new OrderCreated("o-1")));
        await publisher.PublishAsync(new EventEnvelope("orders", "OrderShipped", new OrderShipped("o-1")));

        publisher.Published.Should().HaveCount(2);
        publisher.AllOf<OrderCreated>().Should().HaveCount(1);
        publisher.AssertEventPublished<OrderCreated>();
        publisher.AssertEventCount<OrderShipped>(1);
    }

    [Fact]
    public async Task AssertEventPublished_throws_when_missing()
    {
        var publisher = new EventCapturePublisher();
        Action act = () => publisher.AssertEventPublished<OrderCreated>();
        act.Should().Throw<Xunit.Sdk.XunitException>();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Clear_resets_capture()
    {
        var publisher = new EventCapturePublisher();
        await publisher.PublishAsync(new EventEnvelope("orders", "OrderCreated", new OrderCreated("o-1")));
        publisher.Clear();
        publisher.AssertNoEventsPublished();
    }
}
