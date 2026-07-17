using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Common;

/// <summary>Input model for a personnel-record template.</summary>
public class PersonnelTemplateInput
{
    public PersonnelTemplateKind Kind { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Template HTML body; sanitized in the service.</summary>
    public string ContentHtml { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public int Sorting { get; set; }
}
