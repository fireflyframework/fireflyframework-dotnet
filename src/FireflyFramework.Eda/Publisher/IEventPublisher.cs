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

using FireflyFramework.Eda.Events;

namespace FireflyFramework.Eda.Publisher;

/// <summary>
/// Unified async publisher contract. Mirrors Java <c>EventPublisher</c>: implementations
/// exist for Kafka, RabbitMQ and an in-memory test double.
/// </summary>
public interface IEventPublisher : IAsyncDisposable
{
    PublisherType Type { get; }
    string? DefaultDestination { get; }
    bool IsAvailable { get; }

    Task PublishAsync(EventEnvelope envelope, CancellationToken ct = default);
    Task<PublisherHealth> GetHealthAsync(CancellationToken ct = default);
}

public sealed record PublisherHealth(PublisherType Type, bool Available, string Status, string? Detail = null);
