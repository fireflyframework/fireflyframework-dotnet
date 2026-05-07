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
using FireflyFramework.Client.Discovery;

namespace FireflyFramework.Client.LoadBalancer;

/// <summary>
/// Strategy that picks one instance from a list. Mirrors Java <c>LoadBalancerStrategy</c>.
/// Built-in strategies: <see cref="RoundRobinStrategy"/>, <see cref="WeightedRoundRobinStrategy"/>,
/// <see cref="RandomStrategy"/>, <see cref="LeastConnectionsStrategy"/>,
/// <see cref="StickySessionStrategy"/>, <see cref="ZoneAwareStrategy"/>.
/// </summary>
public interface ILoadBalancerStrategy
{
    ServiceInstance? Select(IReadOnlyList<ServiceInstance> instances);
}

/// <summary>Round-robin: distributes requests evenly using an atomic counter.</summary>
public sealed class RoundRobinStrategy : ILoadBalancerStrategy
{
    private int _counter;

    public ServiceInstance? Select(IReadOnlyList<ServiceInstance> instances)
    {
        if (instances is null || instances.Count == 0) return null;
        var index = Math.Abs(Interlocked.Increment(ref _counter) - 1) % instances.Count;
        return instances[index];
    }
}

/// <summary>Random: uniform random pick using a thread-safe PRNG.</summary>
public sealed class RandomStrategy : ILoadBalancerStrategy
{
    public ServiceInstance? Select(IReadOnlyList<ServiceInstance> instances)
    {
        if (instances is null || instances.Count == 0) return null;
        return instances[Random.Shared.Next(instances.Count)];
    }
}

/// <summary>
/// Weighted round-robin: each instance is repeated <c>weight</c> times in the candidate list.
/// Weights are looked up by <see cref="ServiceInstance.InstanceId"/>; missing entries default
/// to 1.
/// </summary>
public sealed class WeightedRoundRobinStrategy : ILoadBalancerStrategy
{
    private readonly IReadOnlyDictionary<string, int> _weights;
    private int _counter;

    public WeightedRoundRobinStrategy(IReadOnlyDictionary<string, int> weights)
    {
        _weights = weights ?? throw new ArgumentNullException(nameof(weights));
    }

    public ServiceInstance? Select(IReadOnlyList<ServiceInstance> instances)
    {
        if (instances is null || instances.Count == 0) return null;
        var expanded = new List<ServiceInstance>();
        foreach (var inst in instances)
        {
            var weight = _weights.TryGetValue(inst.InstanceId, out var w) ? Math.Max(1, w) : 1;
            for (var i = 0; i < weight; i++) expanded.Add(inst);
        }
        var index = Math.Abs(Interlocked.Increment(ref _counter) - 1) % expanded.Count;
        return expanded[index];
    }
}

/// <summary>
/// Least-connections: tracks active connections per instance via
/// <see cref="IncrementConnections"/> / <see cref="DecrementConnections"/>, then routes
/// the next request to the instance with the lowest active count.
/// </summary>
public sealed class LeastConnectionsStrategy : ILoadBalancerStrategy
{
    private readonly ConcurrentDictionary<string, int> _connections = new();

    public ServiceInstance? Select(IReadOnlyList<ServiceInstance> instances)
    {
        if (instances is null || instances.Count == 0) return null;
        ServiceInstance? best = null;
        var bestCount = int.MaxValue;
        foreach (var inst in instances)
        {
            var count = _connections.GetOrAdd(inst.InstanceId, _ => 0);
            if (count < bestCount)
            {
                best = inst;
                bestCount = count;
            }
        }
        return best;
    }

    public void IncrementConnections(string instanceId) =>
        _connections.AddOrUpdate(instanceId, 1, (_, v) => v + 1);

    public void DecrementConnections(string instanceId) =>
        _connections.AddOrUpdate(instanceId, 0, (_, v) => Math.Max(0, v - 1));
}

/// <summary>
/// Sticky-session: routes the same <c>sessionId</c> to the same instance until that instance
/// disappears from the candidate list. Falls back to <see cref="Select"/> for new sessions
/// or when the previously-pinned instance is no longer available.
/// </summary>
public sealed class StickySessionStrategy : ILoadBalancerStrategy
{
    private readonly ConcurrentDictionary<string, ServiceInstance> _sessions = new();
    private readonly ILoadBalancerStrategy _fallback;

    public StickySessionStrategy(ILoadBalancerStrategy fallback)
    {
        _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
    }

    public ServiceInstance? Select(IReadOnlyList<ServiceInstance> instances) => _fallback.Select(instances);

    public ServiceInstance? SelectWithSession(string sessionId, IReadOnlyList<ServiceInstance> instances)
    {
        if (instances is null || instances.Count == 0) return null;

        if (_sessions.TryGetValue(sessionId, out var existing) && instances.Contains(existing))
        {
            return existing;
        }

        var picked = _fallback.Select(instances);
        if (picked is not null) _sessions[sessionId] = picked;
        return picked;
    }

    public void ClearSession(string sessionId) => _sessions.TryRemove(sessionId, out _);
}

/// <summary>
/// Zone-aware: prefers instances whose <see cref="ServiceInstance.Metadata"/> contains
/// <c>zone == preferredZone</c>. If no such instances exist, falls back to the inner
/// strategy on the full candidate list.
/// </summary>
public sealed class ZoneAwareStrategy : ILoadBalancerStrategy
{
    private readonly string _preferredZone;
    private readonly ILoadBalancerStrategy _fallback;

    public ZoneAwareStrategy(string preferredZone, ILoadBalancerStrategy fallback)
    {
        _preferredZone = preferredZone ?? throw new ArgumentNullException(nameof(preferredZone));
        _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
    }

    public ServiceInstance? Select(IReadOnlyList<ServiceInstance> instances)
    {
        if (instances is null || instances.Count == 0) return null;
        var inZone = instances.Where(i => i.Metadata.TryGetValue("zone", out var z) && z == _preferredZone).ToList();
        return _fallback.Select(inZone.Count > 0 ? inZone : instances);
    }
}
