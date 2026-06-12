using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Termine;

/// <summary>Formular-/Eingabemodell zum Anlegen und Bearbeiten eines Termins.</summary>
public class TerminEingabe
{
    public string Titel { get; set; } = string.Empty;
    public TerminKategorie Kategorie { get; set; } = TerminKategorie.Sonstiges;
    public TerminStatus Status { get; set; } = TerminStatus.Geplant;
    public string? Ort { get; set; }

    /// <summary>Beginn (lokale RP-Zeit; der Dienst rechnet beim Speichern in UTC um). Pflicht.</summary>
    public DateTime? Beginn { get; set; }

    /// <summary>Ende (optional, lokale RP-Zeit).</summary>
    public DateTime? Ende { get; set; }

    public bool Ganztaegig { get; set; }
    public string? Beschreibung { get; set; }

    /// <summary>Sichtbarkeitsstufe: Öffentlich / Eingeschränkt / Privat.</summary>
    public TerminSichtbarkeitsStufe Sichtbarkeit { get; set; } = TerminSichtbarkeitsStufe.Oeffentlich;
}
