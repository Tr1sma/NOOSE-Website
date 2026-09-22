namespace NOOSE_Website.Models.Common;

/// <summary>Create/edit input for a personal text snippet.</summary>
public class TextSnippetInput
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Plain text; placeholders stay raw here and are expanded when the snippet is inserted.</summary>
    public string Text { get; set; } = string.Empty;

    public int Sorting { get; set; }
}
