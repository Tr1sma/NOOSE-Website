using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Common;

/// <summary>Input model for a custom field definition.</summary>
public class CustomFieldDefinitionInput
{
    public string Name { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public CustomFieldType FieldType { get; set; } = CustomFieldType.Text;

    /// <summary>Select options (one per line); only relevant for select fields.</summary>
    public string? Options { get; set; }

    public bool Mandatory { get; set; }
    public int Order { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>A definition paired with its current value for a record.</summary>
public class CustomFieldValueDisplay
{
    public required CustomFieldDefinition Definition { get; init; }

    public string? Value { get; set; }
}
