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

namespace FireflyFramework.Eda.Annotations;

/// <summary>Marks a method whose result should be published as an event. Mirrors <c>@EventPublisher</c>.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class EventPublisherAttribute : Attribute
{
    public string? Destination { get; set; }
    public string? EventType { get; set; }
    public string? Key { get; set; }
    public string? Condition { get; set; }
    public bool Async { get; set; }
    public int TimeoutMs { get; set; } = 5_000;
    public Events.PublisherType PublisherType { get; set; } = Events.PublisherType.Auto;
    public Events.SerializationFormat Serializer { get; set; } = Events.SerializationFormat.Json;
}

/// <summary>Marks a method as an event listener. Mirrors <c>@EventListener</c>.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class EventListenerAttribute : Attribute
{
    public string[] Destinations { get; set; } = Array.Empty<string>();
    public string[] EventTypes { get; set; } = Array.Empty<string>();
    public Events.ConsumerType ConsumerType { get; set; } = Events.ConsumerType.Auto;
    public ErrorHandlingStrategy ErrorStrategy { get; set; } = ErrorHandlingStrategy.LogAndContinue;
    public int MaxRetries { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 1_000;
    public bool AutoAck { get; set; } = true;
    public string? GroupId { get; set; }
    public int Priority { get; set; }
}

public enum ErrorHandlingStrategy { LogAndContinue, Retry, DeadLetterQueue, Throw }

/// <summary>Publishes the method's return value after a successful invocation.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PublishResultAttribute : Attribute
{
    public string Destination { get; set; } = string.Empty;
    public string? EventType { get; set; }
    public Events.PublisherType PublisherType { get; set; } = Events.PublisherType.Auto;
}
