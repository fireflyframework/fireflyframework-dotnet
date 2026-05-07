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

namespace FireflyFramework.EventSourcing.Domain;

/// <summary>
/// Base class for event-sourced aggregates. Mirrors Java <c>AggregateRoot</c>: subclasses
/// emit events with <see cref="ApplyChange"/> and re-hydrate from history with <see cref="LoadFromHistory"/>.
/// Event handlers are conventional <c>private void On(SpecificEvent e)</c> methods.
/// </summary>
public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _uncommitted = new();

    public Guid Id { get; protected set; }
    public string AggregateType => GetType().Name;
    public long Version { get; private set; } = -1;

    public IReadOnlyList<IDomainEvent> UncommittedChanges => _uncommitted;

    protected void ApplyChange(IDomainEvent @event)
    {
        Apply(@event);
        _uncommitted.Add(@event);
    }

    public void LoadFromHistory(IEnumerable<IDomainEvent> history)
    {
        foreach (var @event in history)
        {
            Apply(@event);
            Version++;
        }
    }

    public void MarkChangesAsCommitted()
    {
        Version += _uncommitted.Count;
        _uncommitted.Clear();
    }

    private void Apply(IDomainEvent @event)
    {
        var method = GetType().GetMethod("On",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { @event.GetType() },
            null);
        method?.Invoke(this, new object[] { @event });
    }
}
