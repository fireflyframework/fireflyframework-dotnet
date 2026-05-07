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

using FireflyFramework.Orchestration.Topology;
using Xunit;

namespace FireflyFramework.Tests;

/// <summary>
/// Tests for the topology builder + graph generators. The builder enforces acyclicity at
/// <see cref="TopologyBuilder.Build"/>; the graph supports topological sort and cycle
/// reporting; the generators emit DOT, Mermaid and PlantUML representations.
/// </summary>
public sealed class TopologyTests
{
    [Fact]
    public void Builder_BuildsAcyclicGraph_WithDeclaredEdges()
    {
        var graph = new TopologyBuilder()
            .AddStep("a", "Charge")
            .AddStep("b", "Reserve inventory")
            .AddStep("c", "Notify customer")
            .AddDependency("a", "c")
            .AddDependency("b", "c")
            .Build();

        Assert.Equal(3, graph.Nodes.Count);
        Assert.Equal(2, graph.Edges.Count);

        var sorted = graph.TopologicalSort();
        Assert.Equal("c", sorted.Last());
    }

    [Fact]
    public void Builder_RejectsCycles()
    {
        var builder = new TopologyBuilder()
            .AddDependency("a", "b")
            .AddDependency("b", "c")
            .AddDependency("c", "a");

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("cycle", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Builder_AutomaticallyAddsNodes_WhenAddDependency_ReferencesUnknownStep()
    {
        var graph = new TopologyBuilder()
            .AddDependency("alpha", "beta")
            .Build();

        Assert.Equal(2, graph.Nodes.Count);
        Assert.Contains(graph.Nodes, n => n.Id == "alpha");
        Assert.Contains(graph.Nodes, n => n.Id == "beta");
    }

    [Fact]
    public void TopologyGraphGenerator_DotEmitsNodes_AndEdges_WithLabels()
    {
        var graph = new TopologyBuilder()
            .AddStep("charge", "Charge card")
            .AddStep("ship", "Ship goods")
            .AddDependency("charge", "ship", "if charge succeeds")
            .Build();

        var dot = TopologyGraphGenerator.ToDot(graph, "test");
        Assert.Contains("digraph \"test\"", dot);
        Assert.Contains("\"charge\" -> \"ship\"", dot);
        Assert.Contains("if charge succeeds", dot);
        Assert.Contains("Charge card", dot);
    }

    [Fact]
    public void TopologyGraphGenerator_MermaidUsesFlowchartLR()
    {
        var graph = new TopologyBuilder()
            .AddStep("a")
            .AddStep("b")
            .AddDependency("a", "b")
            .Build();

        var mermaid = TopologyGraphGenerator.ToMermaid(graph);
        Assert.Contains("flowchart LR", mermaid);
        Assert.Contains("a --> b", mermaid);
    }

    [Fact]
    public void TopologyGraphGenerator_PlantUmlBracketsTitle()
    {
        var graph = new TopologyBuilder().AddStep("only").Build();
        var puml = TopologyGraphGenerator.ToPlantUml(graph, "Sample");

        Assert.Contains("@startuml", puml);
        Assert.Contains("title Sample", puml);
        Assert.Contains("@enduml", puml);
    }
}
