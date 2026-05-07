using System.Text;

namespace FireflyFramework.Orchestration.Topology;

/// <summary>
/// Renders a <see cref="TopologyGraph"/> into common visualisation formats. Mirrors Java
/// <c>TopologyGraphGenerator</c>. Three output formats are supported:
///
/// <list type="bullet">
/// <item><see cref="ToDot"/> — Graphviz DOT for <c>dot -Tpng &lt;file&gt;</c>.</item>
/// <item><see cref="ToMermaid"/> — Mermaid flowchart for embedded GitHub / docs rendering.</item>
/// <item><see cref="ToPlantUml"/> — PlantUML activity diagram.</item>
/// </list>
/// </summary>
public static class TopologyGraphGenerator
{
    /// <summary>Emits a Graphviz DOT representation.</summary>
    public static string ToDot(TopologyGraph graph, string title = "topology")
    {
        ArgumentNullException.ThrowIfNull(graph);
        var sb = new StringBuilder();
        sb.AppendLine($"digraph \"{title}\" {{");
        sb.AppendLine("    rankdir=LR;");
        sb.AppendLine("    node [shape=box, style=rounded];");

        foreach (var node in graph.Nodes)
        {
            var label = string.IsNullOrEmpty(node.Description)
                ? node.DisplayName
                : $"{node.DisplayName}\\n({node.Description})";
            sb.AppendLine($"    \"{node.Id}\" [label=\"{Escape(label)}\"];");
        }

        foreach (var edge in graph.Edges)
        {
            if (edge.Condition is not null)
            {
                sb.AppendLine($"    \"{edge.From}\" -> \"{edge.To}\" [label=\"{Escape(edge.Condition)}\"];");
            }
            else
            {
                sb.AppendLine($"    \"{edge.From}\" -> \"{edge.To}\";");
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>Emits a Mermaid <c>flowchart LR</c> representation.</summary>
    public static string ToMermaid(TopologyGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var sb = new StringBuilder();
        sb.AppendLine("flowchart LR");
        foreach (var node in graph.Nodes)
        {
            sb.AppendLine($"    {node.Id}[{node.DisplayName}]");
        }
        foreach (var edge in graph.Edges)
        {
            if (edge.Condition is not null)
            {
                sb.AppendLine($"    {edge.From} -->|{edge.Condition}| {edge.To}");
            }
            else
            {
                sb.AppendLine($"    {edge.From} --> {edge.To}");
            }
        }
        return sb.ToString();
    }

    /// <summary>Emits a PlantUML activity-diagram representation.</summary>
    public static string ToPlantUml(TopologyGraph graph, string title = "Topology")
    {
        ArgumentNullException.ThrowIfNull(graph);
        var sb = new StringBuilder();
        sb.AppendLine("@startuml");
        sb.AppendLine($"title {title}");
        foreach (var node in graph.Nodes)
        {
            sb.AppendLine($"state \"{node.DisplayName}\" as {node.Id}");
        }
        foreach (var edge in graph.Edges)
        {
            if (edge.Condition is not null)
            {
                sb.AppendLine($"{edge.From} --> {edge.To} : {edge.Condition}");
            }
            else
            {
                sb.AppendLine($"{edge.From} --> {edge.To}");
            }
        }
        sb.AppendLine("@enduml");
        return sb.ToString();
    }

    private static string Escape(string s) => s.Replace("\"", "\\\"").Replace("\n", "\\n");
}
