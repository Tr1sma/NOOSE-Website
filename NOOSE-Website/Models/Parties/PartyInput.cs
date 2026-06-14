using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Parties;

/// <summary>Formular-/Eingabemodell zum Anlegen und Bearbeiten einer Partei.</summary>
public class PartyInput
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Targets { get; set; }
    public string? Remarks { get; set; }
    public Classification Classification { get; set; } = Classification.Unknown;
    public string? ClassificationJustification { get; set; }
    public bool IsClassified { get; set; }

    /// <summary>Mitglieder, die bereits beim Anlegen erfasst werden (auf der Detailseite weiter pflegbar).</summary>
    public List<PartyMemberInput> Members { get; set; } = new();
}

/// <summary>Eingabe zum Hinzufügen/Ändern einer Partei-Mitgliedschaft.</summary>
public class PartyMemberInput
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
