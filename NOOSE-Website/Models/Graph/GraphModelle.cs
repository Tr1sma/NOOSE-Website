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
public record GraphNode(
    string Id,
    string Type,
    string Designation,
    string? Subtitle,
    string? Href,
    int ClassificationLevel,
    bool IsClassified,
    string? PhotoUrl,
    int Degree);

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
public record GraphQuery(
    string? FocusType = null,
    string? FocusId = null,
    int Depth = 1,
    IReadOnlyCollection<string>? TypeFilter = null,
    LinkKind? KindFilter = null);

/// <summary>Path search result between two records.</summary>
public record PathResult(
    bool Found,
    IReadOnlyList<GraphNode> Node,
    IReadOnlyList<GraphEdge> Edges);

/// <summary>Record selection in graph UI (focus or path endpoint).</summary>
public record GraphRecordChoice(string Type, string Id, string Designation);

/// <summary>Auto-detected link suggestion; not yet linked.</summary>
public record LinkSuggestion(
    string TargetType,
    string TargetId,
    string Designation,
    string? Subtitle,
    string? Href,
    string Reason,
    int Strength);
