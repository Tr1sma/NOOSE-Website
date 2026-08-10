using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Graph;

/// <summary>Graph node; a resolved visible record.</summary>
/// <param name="Id">Graph key "Type:EntityId".</param>
/// <param name="Type">CLR type name; controls colour/icon.</param>
/// <param name="Designation">Display name.</param>
/// <param name="Subtitle">Optional subtitle.</param>
/// <param name="Href">Detail page link or null.</param>
/// <param name="ClassificationLevel">Security level 0–3.</param>
/// <param name="IsClassified">Classified record badge.</param>
/// <param name="PhotoUrl">Optional photo thumbnail.</param>
/// <param name="Degree">Node degree; controls node size.</param>
/// <param name="IsFocus">The record the viewer picked; rendered highlighted.</param>
public record GraphNode(
    string Id,
    string Type,
    string Designation,
    string? Subtitle,
    string? Href,
    int ClassificationLevel,
    bool IsClassified,
    string? PhotoUrl,
    int Degree,
    DateTime? CreatedAt = null,
    int? ThreatScore = null,
    double Betweenness = 0,
    int CommunityId = 0,
    bool IsKeyFigure = false,
    bool IsFocus = false);

/// <summary>Undirected graph edge between two nodes.</summary>
public record GraphEdge(
    string Source,
    string Target,
    string? Label,
    LinkKind Kind,
    bool Automatic);

/// <summary>Complete graph result; truncated if over node limit.</summary>
public record GraphData(
    IReadOnlyList<GraphNode> Node,
    IReadOnlyList<GraphEdge> Edges,
    bool Truncated);

/// <summary>Graph query parameters.</summary>
/// <param name="FocusType">Focus node type or null for full graph.</param>
/// <param name="FocusId">Focus node id or null for full graph.</param>
/// <param name="Depth">Hop count around focus node (1–3).</param>
/// <param name="TypeFilter">Filter by node types if set.</param>
/// <param name="KindFilter">Filter by edge kind if set.</param>
/// <param name="MarkType">Record to mark visually; independent of the focus radius.</param>
/// <param name="MarkId">Id of the record to mark visually.</param>
public record GraphQuery(
    string? FocusType = null,
    string? FocusId = null,
    int Depth = 1,
    IReadOnlyCollection<string>? TypeFilter = null,
    LinkKind? KindFilter = null,
    bool ComputeCentrality = false,
    bool ComputeCommunities = false,
    string? MarkType = null,
    string? MarkId = null);

/// <summary>Path search result between two records.</summary>
public record PathResult(
    bool Found,
    IReadOnlyList<GraphNode> Node,
    IReadOnlyList<GraphEdge> Edges);

/// <summary>Record selection in graph UI (focus or path endpoint).</summary>
public record GraphRecordChoice(string Type, string Id, string Designation);

/// <summary>Everything a saved graph view restores besides the node positions.</summary>
/// <param name="Kind">Edge-kind filter as the UI stores it ("alle"/"Standard"/"Konflikt"/"Buendnis").</param>
public record GraphViewState(
    bool Centrality = false,
    bool Community = false,
    bool FocusMode = false,
    string? FocusType = null,
    string? FocusId = null,
    string? FocusName = null,
    int Depth = 2,
    IReadOnlyList<string>? Types = null,
    string Kind = "alle")
{
    private static readonly string[] Kinds = { "alle", "Standard", "Konflikt", "Buendnis" };

    /// <summary>Clamps values coming from storage so a hand-edited blob cannot break the query.</summary>
    public GraphViewState Sanitized() => this with
    {
        Depth = Math.Clamp(Depth, 1, 3),
        Kind = Kinds.Contains(Kind) ? Kind : "alle",
    };
}

/// <summary>Auto-detected link suggestion; not yet linked.</summary>
public record LinkSuggestion(
    string TargetType,
    string TargetId,
    string Designation,
    string? Subtitle,
    string? Href,
    string Reason,
    int Strength);
