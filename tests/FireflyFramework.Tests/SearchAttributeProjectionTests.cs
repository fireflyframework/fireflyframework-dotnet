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

using FireflyFramework.Orchestration.Workflow;
using Xunit;

namespace FireflyFramework.Tests;

/// <summary>
/// Tests for the in-memory workflow search projection. Pin the contract: forward and
/// inverted indexes stay consistent under upserts and removals; <see cref="SearchAttributeProjection.FindByAttributes"/>
/// returns the intersection.
/// </summary>
public sealed class SearchAttributeProjectionTests
{
    [Fact]
    public void Upsert_StoresValue_AndIndexesByKey()
    {
        var p = new SearchAttributeProjection();
        p.Upsert("exec-1", "customerId", 12345);
        p.Upsert("exec-1", "region", "eu-west-1");

        Assert.Equal(12345, p.Get("exec-1", "customerId"));
        Assert.Equal("eu-west-1", p.Get("exec-1", "region"));
        Assert.Equal(2, p.GetAll("exec-1").Count);
    }

    [Fact]
    public void FindByAttribute_ReturnsAllExecutionIdsWithMatchingValue()
    {
        var p = new SearchAttributeProjection();
        p.Upsert("exec-1", "region", "eu");
        p.Upsert("exec-2", "region", "eu");
        p.Upsert("exec-3", "region", "us");

        var inEu = p.FindByAttribute("region", "eu");
        Assert.Equal(2, inEu.Count);
        Assert.Contains("exec-1", inEu);
        Assert.Contains("exec-2", inEu);
    }

    [Fact]
    public void Upsert_OverwritesPreviousValue_RemovesOldFromInvertedIndex()
    {
        var p = new SearchAttributeProjection();
        p.Upsert("exec-1", "region", "eu");
        p.Upsert("exec-1", "region", "us");

        Assert.Empty(p.FindByAttribute("region", "eu"));
        Assert.Single(p.FindByAttribute("region", "us"));
    }

    [Fact]
    public void FindByAttributes_ReturnsIntersectionOfMatches()
    {
        var p = new SearchAttributeProjection();
        p.Upsert("e1", "region", "eu"); p.Upsert("e1", "tier", "gold");
        p.Upsert("e2", "region", "eu"); p.Upsert("e2", "tier", "silver");
        p.Upsert("e3", "region", "us"); p.Upsert("e3", "tier", "gold");

        var match = p.FindByAttributes(new Dictionary<string, object> { ["region"] = "eu", ["tier"] = "gold" });
        Assert.Single(match);
        Assert.Contains("e1", match);
    }

    [Fact]
    public void Remove_DeletesEveryAttribute_AndCleansInvertedIndex()
    {
        var p = new SearchAttributeProjection();
        p.Upsert("e1", "region", "eu");
        p.Upsert("e2", "region", "eu");

        p.Remove("e1");

        Assert.Empty(p.GetAll("e1"));
        var match = p.FindByAttribute("region", "eu");
        Assert.Single(match);
        Assert.Contains("e2", match);
    }
}
