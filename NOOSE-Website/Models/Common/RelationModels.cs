using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Common;

/// <summary>Eingabe zum Anlegen einer Person-zu-Person-Beziehung.</summary>
public class RelationInput
{
    public string TargetPersonId { get; set; } = string.Empty;
    public RelationType Type { get; set; } = RelationType.Known;
    public string? Note { get; set; }
}

/// <summary>Aufbereitete Beziehung aus Sicht einer Person: die jeweils andere Person + Typ.</summary>
public record RelationDisplay(
    string RelationId,
    RelationType Type,
    string? Note,
    string OtherPersonId,
    string OtherPersonName,
    string OtherPersonCaseNumber);
