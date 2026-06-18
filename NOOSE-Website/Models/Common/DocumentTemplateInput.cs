namespace NOOSE_Website.Models.Common;

/// <summary>Input model for a document template.</summary>
public class DocumentTemplateInput
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }

    /// <summary>Template HTML body (may contain placeholders like {{Name}}); sanitized in the service.</summary>
    public string ContentHtml { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public int Sorting { get; set; }
}
