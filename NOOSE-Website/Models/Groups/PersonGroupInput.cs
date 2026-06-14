using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Groups;

/// <summary>Formular-/Eingabemodell zum Anlegen und Bearbeiten einer Personengruppe.</summary>
public class PersonGroupInput
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Targets { get; set; }

    /// <summary>Kategorie der Gruppen-Akte (Persönlichkeit/Gruppierung/Person of Interest).</summary>
    public GroupsKind Kind { get; set; } = GroupsKind.Grouping;

    public Classification Classification { get; set; } = Classification.Unknown;
    public string? ClassificationJustification { get; set; }
    public int? EstimatedMemberCount { get; set; }
    public bool IsClassified { get; set; }

    /// <summary>Mitglieder, die bereits beim Anlegen erfasst werden (auf der Detailseite weiter pflegbar).</summary>
    public List<GroupMemberInput> Members { get; set; } = new();
}

/// <summary>Eingabe zum Hinzufügen/Ändern einer Gruppen-Mitgliedschaft.</summary>
public class GroupMemberInput
{
    public string PersonId { get; set; } = string.Empty;
    public string? Role { get; set; }
    public bool IsLead { get; set; }

    /// <summary>Nur für die Anzeige im Anlege-Formular; vom Dienst ignoriert.</summary>
    public string? PersonName { get; set; }

    /// <summary>
    /// Ist <see cref="PersonId"/> leer und dies gesetzt, wird beim Hinzufügen automatisch eine neue
    /// Personen-Akte mit diesem Namen angelegt und als Mitglied verknüpft.
    /// </summary>
    public string? NewPersonName { get; set; }
}

/// <summary>Erfassungsfortschritt einer Gruppe: erfasste Mitglieder (x) gegenüber geschätzter Gesamtgröße (y).</summary>
public record PersonGroupProgress(int Captured, int? Estimated);
