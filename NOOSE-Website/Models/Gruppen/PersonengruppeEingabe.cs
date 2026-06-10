using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Gruppen;

/// <summary>Formular-/Eingabemodell zum Anlegen und Bearbeiten einer Personengruppe.</summary>
public class PersonengruppeEingabe
{
    public string Name { get; set; } = string.Empty;
    public string? Beschreibung { get; set; }
    public string? Ziele { get; set; }

    /// <summary>Kategorie der Gruppen-Akte (Persönlichkeit/Gruppierung/Person of Interest).</summary>
    public GruppenArt Art { get; set; } = GruppenArt.Gruppierung;

    public Einstufung Einstufung { get; set; } = Einstufung.Unbekannt;
    public string? EinstufungBegruendung { get; set; }
    public int? GeschaetzteMitgliederzahl { get; set; }
    public bool IstVerschlusssache { get; set; }

    /// <summary>Mitglieder, die bereits beim Anlegen erfasst werden (auf der Detailseite weiter pflegbar).</summary>
    public List<GruppeMitgliedEingabe> Mitglieder { get; set; } = new();
}

/// <summary>Eingabe zum Hinzufügen/Ändern einer Gruppen-Mitgliedschaft.</summary>
public class GruppeMitgliedEingabe
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

/// <summary>Erfassungsfortschritt einer Gruppe: erfasste Mitglieder (x) gegenüber geschätzter Gesamtgröße (y).</summary>
public record PersonengruppeFortschritt(int Erfasst, int? Geschaetzt);
