namespace NOOSE_Website.Models.Common;

/// <summary>Input model for a tag.</summary>
public class TagInput
{
    public string Name { get; set; } = string.Empty;

    /// <summary>MudBlazor color name; empty means default color.</summary>
    public string? Colour { get; set; }
}
