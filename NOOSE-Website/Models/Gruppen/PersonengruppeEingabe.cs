using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Gruppen;

/// <summary>Formular-/Eingabemodell zum Anlegen und Bearbeiten einer Personengruppe.</summary>
public class PersonengruppeEingabe
{
    public string Name { get; set; } = string.Empty;
    public string? Beschreibung { get; set; }
    public string? Ziele { get; set; }
    public Einstufung Einstufung { get; set; } = Einstufung.Unbekannt;
    public string? EinstufungBegruendung { get; set; }
    public int? GeschaetzteMitgliederzahl { get; set; }
    public bool IstVerschlusssache { get; set; }
}

/// <summary>Eingabe zum Hinzufügen/Ändern einer Gruppen-Mitgliedschaft.</summary>
public class GruppeMitgliedEingabe
{
    public string PersonId { get; set; } = string.Empty;
    public string? Rolle { get; set; }
    public bool IstLeitung { get; set; }
}

/// <summary>Erfassungsfortschritt einer Gruppe: erfasste Mitglieder (x) gegenüber geschätzter Gesamtgröße (y).</summary>
public record PersonengruppeFortschritt(int Erfasst, int? Geschaetzt);
