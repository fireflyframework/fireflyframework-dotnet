namespace FireflyFramework.Orchestration.Topology;

/// <summary>
/// Builds a directed acyclic graph from declared step dependencies. Mirrors Java
/// <c>TopologyBuilder</c>. The output is a <see cref="TopologyGraph"/> that downstream
/// engines can topologically sort, render, or feed to <see cref="TopologyGraphGenerator"/>.
///
/// <para>This class is intentionally engine-agnostic — both saga and workflow topologies
/// are described by edges of the form <c>(from-step, to-step, condition?)</c>.</para>
/// </summary>
public sealed class TopologyBuilder
{
    private readonly Dictionary<string, TopologyNode> _nodes = new();
    private readonly List<TopologyEdge> _edges = new();

    /// <summary>Adds a step node. Idempotent — re-adding a node updates its display name.</summary>
    public TopologyBuilder AddStep(string id, string? displayName = null, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        _nodes[id] = new TopologyNode(id, displayName ?? id, description);
        return this;
    }

    /// <summary>
    /// Adds a directed dependency: <paramref name="from"/> must complete before
    /// <paramref name="to"/> may start. <paramref name="condition"/> is an optional
    /// human-readable label (e.g. "if amount &gt; 1000").
    /// </summary>
    public TopologyBuilder AddDependency(string from, string to, string? condition = null)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        if (!_nodes.ContainsKey(from)) AddStep(from);
        if (!_nodes.ContainsKey(to)) AddStep(to);
        _edges.Add(new TopologyEdge(from, to, condition));
        return this;
    }

    /// <summary>
    /// Builds the immutable graph. Throws <see cref="InvalidOperationException"/> if the
    /// graph contains a cycle (orchestration topologies must be acyclic).
    /// </summary>
    public TopologyGraph Build()
    {
        var graph = new TopologyGraph(_nodes.Values.ToList(), _edges.ToList());
        var cycle = graph.FindCycle();
        if (cycle is not null)
        {
            throw new InvalidOperationException($"Topology contains a cycle: {string.Join(" → ", cycle)}");
        }
        return graph;
    }
}

/// <summary>One step / activity in the topology.</summary>
public sealed record TopologyNode(string Id, string DisplayName, string? Description);

/// <summary>A directed dependency between two steps.</summary>
public sealed record TopologyEdge(string From, string To, string? Condition);

/// <summary>An immutable view of the topology with traversal helpers.</summary>
public sealed class TopologyGraph
{
    public IReadOnlyList<TopologyNode> Nodes { get; }
    public IReadOnlyList<TopologyEdge> Edges { get; }

    public TopologyGraph(IReadOnlyList<TopologyNode> nodes, IReadOnlyList<TopologyEdge> edges)
    {
        Nodes = nodes;
        Edges = edges;
    }

    /// <summary>Returns the topologically-sorted node IDs, or throws if the graph has a cycle.</summary>
    public IReadOnlyList<string> TopologicalSort()
    {
        var indegree = Nodes.ToDictionary(n => n.Id, _ => 0);
        var adjacency = Nodes.ToDictionary(n => n.Id, _ => new List<string>());
        foreach (var e in Edges)
        {
            indegree[e.To] = indegree.GetValueOrDefault(e.To) + 1;
            adjacency[e.From].Add(e.To);
        }

        var queue = new Queue<string>(indegree.Where(p => p.Value == 0).Select(p => p.Key));
        var result = new List<string>();
        while (queue.Count > 0)
        {
            var n = queue.Dequeue();
            result.Add(n);
            foreach (var next in adjacency[n])
            {
                if (--indegree[next] == 0) queue.Enqueue(next);
            }
        }

        if (result.Count != Nodes.Count) throw new InvalidOperationException("Topology contains a cycle.");
        return result;
    }

    /// <summary>Returns the cycle as a list of node IDs, or <c>null</c> if the graph is acyclic.</summary>
    public IReadOnlyList<string>? FindCycle()
    {
        var visited = new Dictionary<string, int>(); // 0=unvisited, 1=in-stack, 2=done
        var stack = new List<string>();

        foreach (var node in Nodes)
        {
            if (DfsCycle(node.Id, visited, stack)) return stack.ToList();
        }
        return null;
    }

    private bool DfsCycle(string id, Dictionary<string, int> visited, List<string> stack)
    {
        if (visited.GetValueOrDefault(id) == 1) return true;
        if (visited.GetValueOrDefault(id) == 2) return false;

        visited[id] = 1;
        stack.Add(id);
        foreach (var e in Edges.Where(e => e.From == id))
        {
            if (DfsCycle(e.To, visited, stack)) return true;
        }
        stack.RemoveAt(stack.Count - 1);
        visited[id] = 2;
        return false;
    }
}
