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

using FireflyFramework.Eda.Configuration;
using FireflyFramework.Eda.Consumer;
using FireflyFramework.Eda.Events;
using FireflyFramework.Eda.Publisher;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FireflyFramework.Tests;

public class EdaTests
{
    [Fact]
    public async Task InMemory_publish_then_consume_round_trips_envelope()
    {
        var bus = new InMemoryEventBus();
        var publisher = new InMemoryEventPublisher(bus);
        var consumer = new InMemoryEventConsumer(bus);

        await consumer.StartAsync();
        await publisher.PublishAsync(EventEnvelope.ForPublishing("topic.a", "Created", new { id = 1 }));
        await publisher.PublishAsync(EventEnvelope.ForPublishing("topic.a", "Updated", new { id = 2 }));

        var seen = new List<EventEnvelope>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            await foreach (var env in consumer.ConsumeAsync(new[] { "topic.a" }, cts.Token))
            {
                seen.Add(env);
                if (seen.Count == 2) break;
            }
        }
        catch (OperationCanceledException) { /* fine */ }

        seen.Should().HaveCount(2);
        seen.Select(e => e.EventType).Should().BeEquivalentTo(new[] { "Created", "Updated" });
    }

    [Fact]
    public async Task InMemory_publisher_health_is_up()
    {
        var bus = new InMemoryEventBus();
        var publisher = new InMemoryEventPublisher(bus);
        var health = await publisher.GetHealthAsync();
        health.Available.Should().BeTrue();
        health.Status.Should().Be("UP");
    }

    [Fact]
    public async Task InMemory_consumer_reports_running_state()
    {
        var bus = new InMemoryEventBus();
        var consumer = new InMemoryEventConsumer(bus);

        (await consumer.GetHealthAsync()).Running.Should().BeFalse();
        await consumer.StartAsync();
        (await consumer.GetHealthAsync()).Running.Should().BeTrue();
        await consumer.StopAsync();
        (await consumer.GetHealthAsync()).Running.Should().BeFalse();
    }

    [Fact]
    public void EventEnvelope_for_publishing_sets_timestamp()
    {
        var env = EventEnvelope.ForPublishing("d", "Created", new { });
        env.Destination.Should().Be("d");
        env.EventType.Should().Be("Created");
        env.Timestamp.Should().BeAfter(DateTimeOffset.UtcNow.AddSeconds(-5));
    }

    [Fact]
    public void EventEnvelope_with_headers_returns_new_record()
    {
        var env = EventEnvelope.ForPublishing("d", "T", new { });
        var withHeaders = env.WithHeaders(new Dictionary<string, string> { ["a"] = "b" });
        withHeaders.Headers!["a"].Should().Be("b");
        env.Headers.Should().BeNull();
    }

    [Fact]
    public async Task KafkaConsumer_health_reports_down_before_start()
    {
        var consumer = new KafkaEventConsumer(
            Options.Create(new EdaOptions()),
            NullLogger<KafkaEventConsumer>.Instance);

        var health = await consumer.GetHealthAsync();
        health.Running.Should().BeFalse();
        health.Status.Should().Be("DOWN");
    }
}
