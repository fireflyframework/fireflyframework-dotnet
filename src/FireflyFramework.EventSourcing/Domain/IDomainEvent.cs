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

using System.Reflection;
using FireflyFramework.EventSourcing.Annotations;

namespace FireflyFramework.EventSourcing.Domain;

/// <summary>Domain event contract. Mirrors Java <c>Event</c>.</summary>
public interface IDomainEvent
{
    Guid AggregateId { get; }
    DateTimeOffset Timestamp { get; }

    string EventType => GetType().GetCustomAttribute<DomainEventAttribute>()?.EventType ?? GetType().Name;

    int EventVersion => GetType().GetCustomAttribute<DomainEventAttribute>()?.Version ?? 1;

    Dictionary<string, object?>? Metadata => null;
}

public abstract record AbstractDomainEvent(Guid AggregateId, DateTimeOffset Timestamp) : IDomainEvent;
