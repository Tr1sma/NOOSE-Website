namespace NOOSE_Website.Models.Common;

/// <summary>Input for a generic link to any target record.</summary>
public class LinkInput
{
    /// <summary>Target record type; defaults to Person.</summary>
    public string TargetType { get; set; } = "Person";
    public string TargetId { get; set; } = string.Empty;
    public string? Label { get; set; }

    /// <summary>UI-only display name set by the picker; not persisted.</summary>
    public string? Display { get; set; }
}

/// <summary>A link from one record's perspective: the other side plus its designation.</summary>
public record LinkDisplay(
    string LinkId,
    string OtherType,
    string OtherId,
    string? Label,
    string OtherDesignation,
    bool Automatic = false,
    string? Href = null);
