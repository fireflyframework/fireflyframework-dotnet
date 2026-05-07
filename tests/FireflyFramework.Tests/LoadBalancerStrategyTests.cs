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

using FireflyFramework.Client.Discovery;
using FireflyFramework.Client.LoadBalancer;
using Xunit;

namespace FireflyFramework.Tests;

/// <summary>
/// Tests for the load-balancer strategies. Each strategy is exercised against a fixed
/// 3-instance pool and asserted on its specific behaviour (round-robin order, weighted
/// distribution, sticky-session pinning, zone preference, least-connections selection).
/// </summary>
public sealed class LoadBalancerStrategyTests
{
    private static IReadOnlyList<ServiceInstance> Pool(params string[] zones) => zones
        .Select((z, i) => new ServiceInstance(
            $"id-{i}", "svc", $"h{i}", 80, false, HealthStatus.Up,
            new Dictionary<string, string> { ["zone"] = z }))
        .ToList();

    [Fact]
    public void RoundRobin_CyclesInOrder()
    {
        var lb = new RoundRobinStrategy();
        var pool = Pool("a", "b", "c");

        var picks = Enumerable.Range(0, 6).Select(_ => lb.Select(pool)!.InstanceId).ToList();
        Assert.Equal(new[] { "id-0", "id-1", "id-2", "id-0", "id-1", "id-2" }, picks);
    }

    [Fact]
    public void RoundRobin_EmptyPool_ReturnsNull()
    {
        Assert.Null(new RoundRobinStrategy().Select(Array.Empty<ServiceInstance>()));
    }

    [Fact]
    public void Random_AlwaysReturnsAPoolMember()
    {
        var lb = new RandomStrategy();
        var pool = Pool("a", "b", "c");
        for (var i = 0; i < 50; i++)
        {
            var picked = lb.Select(pool);
            Assert.NotNull(picked);
            Assert.Contains(picked, pool);
        }
    }

    [Fact]
    public void WeightedRoundRobin_SkewsByWeight()
    {
        var pool = Pool("a", "b", "c");
        var lb = new WeightedRoundRobinStrategy(new Dictionary<string, int>
        {
            ["id-0"] = 3, ["id-1"] = 1, ["id-2"] = 1,
        });

        var counts = new Dictionary<string, int> { ["id-0"] = 0, ["id-1"] = 0, ["id-2"] = 0 };
        for (var i = 0; i < 500; i++) counts[lb.Select(pool)!.InstanceId]++;

        // id-0 has weight 3 of 5 = 60% of the picks; allow a wide tolerance.
        Assert.True(counts["id-0"] > counts["id-1"] * 2);
        Assert.True(counts["id-0"] > counts["id-2"] * 2);
    }

    [Fact]
    public void LeastConnections_PicksInstanceWithFewestConnections()
    {
        var pool = Pool("a", "b", "c");
        var lb = new LeastConnectionsStrategy();
        lb.IncrementConnections("id-0");
        lb.IncrementConnections("id-0");
        lb.IncrementConnections("id-1");

        Assert.Equal("id-2", lb.Select(pool)!.InstanceId);

        lb.DecrementConnections("id-0");
        lb.DecrementConnections("id-0");
        Assert.Equal("id-0", lb.Select(pool)!.InstanceId);
    }

    [Fact]
    public void StickySession_PinsBySessionId()
    {
        var pool = Pool("a", "b", "c");
        var lb = new StickySessionStrategy(new RoundRobinStrategy());

        var firstA = lb.SelectWithSession("session-A", pool);
        var firstB = lb.SelectWithSession("session-B", pool);

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(firstA, lb.SelectWithSession("session-A", pool));
            Assert.Equal(firstB, lb.SelectWithSession("session-B", pool));
        }

        lb.ClearSession("session-A");
        // Without an entry, the fallback advances; not asserting equality, just that it's a valid pool member.
        Assert.Contains(lb.SelectWithSession("session-A", pool)!, pool);
    }

    [Fact]
    public void ZoneAware_PrefersInZoneInstances()
    {
        var pool = Pool("eu-west-1", "us-east-1", "eu-west-1");
        var lb = new ZoneAwareStrategy("eu-west-1", new RoundRobinStrategy());

        for (var i = 0; i < 6; i++)
        {
            var picked = lb.Select(pool);
            Assert.Equal("eu-west-1", picked!.Metadata["zone"]);
        }
    }

    [Fact]
    public void ZoneAware_NoZoneMatch_FallsBackToFullPool()
    {
        var pool = Pool("eu-west-1", "us-east-1");
        var lb = new ZoneAwareStrategy("ap-south-1", new RoundRobinStrategy());

        Assert.Contains(lb.Select(pool)!, pool);
    }
}
