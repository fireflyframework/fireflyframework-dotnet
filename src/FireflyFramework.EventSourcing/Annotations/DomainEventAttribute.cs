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

namespace FireflyFramework.EventSourcing.Annotations;

/// <summary>
/// Tags a class as a domain event. Equivalent to Java <c>@DomainEvent</c>: the
/// <see cref="EventType"/> is used as the persisted discriminator.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class DomainEventAttribute : Attribute
{
    public DomainEventAttribute(string eventType) => EventType = eventType;
    public string EventType { get; }
    public string? Description { get; set; }
    public int Version { get; set; } = 1;
    public bool Publishable { get; set; } = true;
    public string[] Tags { get; set; } = Array.Empty<string>();
}
