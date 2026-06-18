using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Common;

/// <summary>Input for a source/attachment; relevant fields vary by type. Upload buffers the file in memory so no browser stream outlives the dialog.</summary>
public class SourceInput
{
    public SourceType Type { get; set; } = SourceType.Link;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Url { get; set; }

    public string? TargetType { get; set; }
    public string? TargetId { get; set; }

    /// <summary>Dialog-only: create a new document in the editor instead of referencing an existing one.</summary>
    public bool NewDocumentCreate { get; set; }

    /// <summary>Taskforce-internal: hide from non-members.</summary>
    public bool IsInternalOnly { get; set; }

    public byte[]? FileContent { get; set; }
    public string? OriginalName { get; set; }
    public string? ContentType { get; set; }
    public long SizeBytes { get; set; }
}
