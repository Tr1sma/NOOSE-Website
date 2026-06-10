using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Parteien;

/// <summary>Formular-/Eingabemodell zum Anlegen und Bearbeiten einer Partei.</summary>
public class ParteiEingabe
{
    public string Name { get; set; } = string.Empty;
    public string? Beschreibung { get; set; }
    public string? Ziele { get; set; }
    public string? Bemerkungen { get; set; }
    public Einstufung Einstufung { get; set; } = Einstufung.Unbekannt;
    public string? EinstufungBegruendung { get; set; }
    public bool IstVerschlusssache { get; set; }

    /// <summary>Mitglieder, die bereits beim Anlegen erfasst werden (auf der Detailseite weiter pflegbar).</summary>
    public List<ParteiMitgliedEingabe> Mitglieder { get; set; } = new();
}

/// <summary>Eingabe zum Hinzufügen/Ändern einer Partei-Mitgliedschaft.</summary>
public class ParteiMitgliedEingabe
{
    public string PersonId { get; set; } = string.Empty;
    public string? Rolle { get; set; }
    public bool IstLeitung { get; set; }

    /// <summary>Nur für die Anzeige im Anlege-Formular; vom Dienst ignoriert.</summary>
    public string? PersonName { get; set; }

    /// <summary>
    /// Ist <see cref="PersonId"/> leer und dies gesetzt, wird beim Hinzufügen automatisch eine neue
    /// Personen-Akte mit diesem Namen angelegt und als Mitglied verknüpft.
    /// </summary>
    public string? NeuePersonName { get; set; }
}
