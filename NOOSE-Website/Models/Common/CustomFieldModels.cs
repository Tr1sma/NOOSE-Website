using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Common;

/// <summary>Formular-/Eingabemodell zum Anlegen/Bearbeiten einer Custom-Feld-Definition (Admin).</summary>
public class CustomFieldDefinitionInput
{
    public string Name { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public CustomFieldType FieldType { get; set; } = CustomFieldType.Text;

    /// <summary>Auswahl-Optionen (eine pro Zeile), nur bei <see cref="CustomFeldTyp.Auswahl"/> relevant.</summary>
    public string? Options { get; set; }

    public bool Mandatory { get; set; }
    public int Order { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Eine Definition samt aktuellem Wert einer Akte – Grundlage für das Zusatzfelder-Panel.</summary>
public class CustomFieldValueDisplay
{
    public required CustomFieldDefinition Definition { get; init; }

    /// <summary>Aktueller gespeicherter Wert (String) bzw. null, wenn noch nicht erfasst.</summary>
    public string? Value { get; set; }
}
