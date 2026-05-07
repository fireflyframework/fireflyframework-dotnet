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

using FireflyFramework.EventSourcing.Store;

namespace FireflyFramework.EventSourcing.Upcasting;

/// <summary>
/// Migrates a stored event payload from one schema version to the next as the
/// aggregate's event shape evolves over time. Mirrors Java <c>EventUpcaster</c>.
/// </summary>
public interface IEventUpcaster
{
    string EventType { get; }
    int FromVersion { get; }
    int ToVersion { get; }
    StoredEventEnvelope Upcast(StoredEventEnvelope envelope);
}

/// <summary>
/// Pipeline that runs every applicable upcaster for an event in the right order
/// (FromVersion → ToVersion → ...) until the event reaches the latest schema.
/// </summary>
public sealed class EventUpcastingService
{
    private readonly IReadOnlyList<IEventUpcaster> _upcasters;

    public EventUpcastingService(IEnumerable<IEventUpcaster> upcasters)
    {
        _upcasters = upcasters.ToList();
    }

    public StoredEventEnvelope Apply(StoredEventEnvelope envelope)
    {
        var current = envelope;
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var upcaster in _upcasters)
            {
                if (upcaster.EventType == current.EventType && upcaster.FromVersion == current.EventVersion)
                {
                    current = upcaster.Upcast(current);
                    changed = true;
                    break;
                }
            }
        }

        return current;
    }
}
