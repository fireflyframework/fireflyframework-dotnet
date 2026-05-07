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

using System.Collections.Concurrent;

namespace FireflyFramework.Orchestration.Workflow;

/// <summary>
/// In-memory inverted-index over user-defined search attributes for workflow / saga / TCC
/// executions. Mirrors Java <c>SearchAttributeProjection</c>. Two indexes are maintained
/// concurrently:
///
/// <list type="bullet">
/// <item><c>correlationId → (key → value)</c> — the forward index for read-by-id queries.</item>
/// <item><c>key → (value → set of correlationIds)</c> — the inverted index for predicate
///       queries (<see cref="FindByAttribute"/>, <see cref="FindByAttributes"/>).</item>
/// </list>
///
/// <para>Useful for building dashboards that filter by business attributes ("show all
/// orders for customer 12345 over $1000"). Backed by <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// for thread-safe upserts; intended for moderate workloads (single-host search). A
/// production system would persist these projections to an external store.</para>
/// </summary>
public sealed class SearchAttributeProjection
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, object?>> _forward = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<object, HashSet<string>>> _inverted = new();
    private readonly object _invertedLock = new();

    /// <summary>
    /// Sets <c>(key, value)</c> for an execution. If the execution previously had a different
    /// value for this key, the old entry is removed from the inverted index first.
    /// </summary>
    public void Upsert(string correlationId, string key, object? value)
    {
        ArgumentNullException.ThrowIfNull(correlationId);
        ArgumentNullException.ThrowIfNull(key);

        var attrs = _forward.GetOrAdd(correlationId, _ => new ConcurrentDictionary<string, object?>());
        attrs.TryGetValue(key, out var oldValue);
        attrs[key] = value;

        lock (_invertedLock)
        {
            if (oldValue is not null && _inverted.TryGetValue(key, out var oldValues) && oldValues.TryGetValue(oldValue, out var oldSet))
            {
                oldSet.Remove(correlationId);
                if (oldSet.Count == 0) oldValues.TryRemove(oldValue, out _);
            }

            if (value is not null)
            {
                var values = _inverted.GetOrAdd(key, _ => new ConcurrentDictionary<object, HashSet<string>>());
                var set = values.GetOrAdd(value, _ => new HashSet<string>());
                set.Add(correlationId);
            }
        }
    }

    /// <summary>Returns the value of one attribute, or <c>null</c> if not set.</summary>
    public object? Get(string correlationId, string key) =>
        _forward.TryGetValue(correlationId, out var attrs) && attrs.TryGetValue(key, out var v) ? v : null;

    /// <summary>Returns every attribute on the execution.</summary>
    public IReadOnlyDictionary<string, object?> GetAll(string correlationId) =>
        _forward.TryGetValue(correlationId, out var attrs)
            ? new Dictionary<string, object?>(attrs)
            : new Dictionary<string, object?>();

    /// <summary>Finds every correlationId whose <paramref name="key"/> equals <paramref name="value"/>.</summary>
    public IReadOnlySet<string> FindByAttribute(string key, object value)
    {
        if (!_inverted.TryGetValue(key, out var values)) return new HashSet<string>();
        if (!values.TryGetValue(value, out var set)) return new HashSet<string>();
        lock (_invertedLock) return new HashSet<string>(set);
    }

    /// <summary>
    /// Finds the intersection — every correlationId that matches every key/value pair.
    /// Empty <paramref name="criteria"/> returns the empty set.
    /// </summary>
    public IReadOnlySet<string> FindByAttributes(IReadOnlyDictionary<string, object> criteria)
    {
        if (criteria is null || criteria.Count == 0) return new HashSet<string>();

        HashSet<string>? intersection = null;
        foreach (var (key, value) in criteria)
        {
            var matches = FindByAttribute(key, value);
            if (intersection is null)
            {
                intersection = new HashSet<string>(matches);
            }
            else
            {
                intersection.IntersectWith(matches);
            }
            if (intersection.Count == 0) return new HashSet<string>();
        }
        return intersection ?? new HashSet<string>();
    }

    /// <summary>Removes every attribute belonging to an execution (typically called on completion).</summary>
    public void Remove(string correlationId)
    {
        if (!_forward.TryRemove(correlationId, out var attrs)) return;
        lock (_invertedLock)
        {
            foreach (var (key, value) in attrs)
            {
                if (value is null || !_inverted.TryGetValue(key, out var values)) continue;
                if (values.TryGetValue(value, out var set))
                {
                    set.Remove(correlationId);
                    if (set.Count == 0) values.TryRemove(value, out _);
                }
            }
        }
    }
}
