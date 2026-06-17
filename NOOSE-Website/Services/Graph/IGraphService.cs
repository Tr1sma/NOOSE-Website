using System.Security.Claims;
using NOOSE_Website.Models.Graph;

namespace NOOSE_Website.Services;

/// <summary>Read-only relationship graph built from links and person relations; all nodes pass central visibility.</summary>
public interface IGraphService
{
    /// <summary>Returns the graph: capped full graph without focus, or the focus node's neighbourhood up to the given depth.</summary>
    Task<GraphData> GetGraphAsync(GraphQuery query, ClaimsPrincipal viewer, CancellationToken cancellationToken = default);

    /// <summary>Finds the shortest visible path between two records; returns not-found when none exists.</summary>
    Task<PathResult> FindPathAsync(string sourceType, string sourceId, string targetType, string targetId, ClaimsPrincipal viewer, CancellationToken cancellationToken = default);
}
