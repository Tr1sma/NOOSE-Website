namespace NOOSE_Website.Services;

/// <summary>Pure graph-analytics helpers (no DB): betweenness centrality + community detection.</summary>
public static class GraphAnalytics
{
    /// <summary>Brandes betweenness for an undirected, unweighted graph, normalized to 0..1 by the maximum.</summary>
    public static Dictionary<string, double> Betweenness(
        IReadOnlyList<string> nodes, IReadOnlyList<(string A, string B)> edges)
    {
        var bc = nodes.ToDictionary(n => n, _ => 0.0);
        if (nodes.Count < 3)
        {
            return bc; // no node lies between others
        }
        var adj = BuildAdjacency(nodes, edges);

        foreach (var s in nodes)
        {
            var stack = new Stack<string>();
            var pred = nodes.ToDictionary(n => n, _ => new List<string>());
            var sigma = nodes.ToDictionary(n => n, _ => 0.0);
            var dist = nodes.ToDictionary(n => n, _ => -1);
            sigma[s] = 1;
            dist[s] = 0;
            var queue = new Queue<string>();
            queue.Enqueue(s);

            while (queue.Count > 0)
            {
                var v = queue.Dequeue();
                stack.Push(v);
                foreach (var w in adj[v])
                {
                    if (dist[w] < 0)
                    {
                        dist[w] = dist[v] + 1;
                        queue.Enqueue(w);
                    }
                    if (dist[w] == dist[v] + 1)
                    {
                        sigma[w] += sigma[v];
                        pred[w].Add(v);
                    }
                }
            }

            var delta = nodes.ToDictionary(n => n, _ => 0.0);
            while (stack.Count > 0)
            {
                var w = stack.Pop();
                foreach (var v in pred[w])
                {
                    delta[v] += sigma[w] == 0 ? 0 : sigma[v] / sigma[w] * (1 + delta[w]);
                }
                if (!w.Equals(s, StringComparison.Ordinal))
                {
                    bc[w] += delta[w];
                }
            }
        }

        // undirected: each shortest path counted twice
        foreach (var n in nodes)
        {
            bc[n] /= 2.0;
        }
        var max = bc.Values.DefaultIfEmpty(0).Max();
        if (max > 0)
        {
            foreach (var n in nodes)
            {
                bc[n] /= max;
            }
        }
        return bc;
    }

    /// <summary>Deterministic label-propagation communities; labels renumbered to 0..k in first-seen order.</summary>
    public static Dictionary<string, int> Communities(
        IReadOnlyList<string> nodes, IReadOnlyList<(string A, string B)> edges)
    {
        var adj = BuildAdjacency(nodes, edges);
        var label = new Dictionary<string, string>();
        foreach (var n in nodes)
        {
            label[n] = n; // each node its own label
        }

        // fixed node order → deterministic convergence
        var order = nodes.ToList();
        for (var iter = 0; iter < 20; iter++)
        {
            var changed = false;
            foreach (var v in order)
            {
                if (adj[v].Count == 0)
                {
                    continue;
                }
                var counts = new Dictionary<string, int>();
                foreach (var w in adj[v])
                {
                    counts[label[w]] = counts.GetValueOrDefault(label[w]) + 1;
                }
                // most frequent neighbour label; on a tie keep the current label if it is among the leaders
                // (prevents a single lexicographically-small label from collapsing the whole graph)
                var maxCount = counts.Values.Max();
                var tied = counts.Where(k => k.Value == maxCount).Select(k => k.Key).ToList();
                var best = tied.Contains(label[v]) ? label[v] : tied.OrderBy(x => x, StringComparer.Ordinal).First();
                if (!label[v].Equals(best, StringComparison.Ordinal))
                {
                    label[v] = best;
                    changed = true;
                }
            }
            if (!changed)
            {
                break;
            }
        }

        var renum = new Dictionary<string, int>();
        var result = new Dictionary<string, int>();
        foreach (var n in nodes)
        {
            if (!renum.TryGetValue(label[n], out var id))
            {
                id = renum.Count;
                renum[label[n]] = id;
            }
            result[n] = id;
        }
        return result;
    }

    /// <summary>A node is a key figure if its normalized betweenness reaches at least half the maximum (and is non-zero).</summary>
    public static bool IsKey(double normalizedBetweenness) => normalizedBetweenness > 0 && normalizedBetweenness >= 0.5;

    private static Dictionary<string, List<string>> BuildAdjacency(
        IReadOnlyList<string> nodes, IReadOnlyList<(string A, string B)> edges)
    {
        var adj = nodes.ToDictionary(n => n, _ => new List<string>());
        foreach (var (a, b) in edges)
        {
            if (a.Equals(b, StringComparison.Ordinal) || !adj.ContainsKey(a) || !adj.ContainsKey(b))
            {
                continue;
            }
            adj[a].Add(b);
            adj[b].Add(a);
        }
        return adj;
    }
}
