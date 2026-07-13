namespace NOOSE_Website.Models.Common;

/// <summary>Input model for an activity template.</summary>
public class ActivityTemplateInput
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Optional default activity kind applied when the template is picked.</summary>
    public string? Kind { get; set; }

    /// <summary>Template HTML body; sanitized in the service.</summary>
    public string ContentHtml { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public int Sorting { get; set; }
}
