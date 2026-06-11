using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Data.Entities.Personen;

/// <summary>
/// Admin-/Führungs-definierte Erfassungsmaske („Vorlage") für ein Personen-Dok. Hält Default-Werte für
/// die Dok-Felder, mit denen das Anlege-Formular vorbefüllt wird (frei editierbar). Setzt die „Vorgaben"
/// um (Plan.md Phase 7). Voll auditiert und papierkorbfähig.
/// </summary>
public class DokVorlage : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Sprechender Name der Vorlage, z. B. „Verhör – Standard". Eindeutig.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optionale Erläuterung – wird in der Verwaltung und im Vorlagen-Picker angezeigt.</summary>
    public string? Beschreibung { get; set; }

    /// <summary>Nur aktive Vorlagen erscheinen im Picker beim Dok-Anlegen.</summary>
    public bool IstAktiv { get; set; } = true;

    /// <summary>Sortierreihenfolge im Picker/der Liste (kleiner zuerst).</summary>
    public int Sortierung { get; set; }

    // ---- Default-Werte für die Dok-Felder (gespiegelte editierbare Teilmenge von PersonDok) ----
    public string? StandardGrund { get; set; }

    /// <summary>Default für die Fraktionszugehörigkeit als Freitext (Org-Verknüpfung ist instanzspezifisch).</summary>
    public string? StandardFraktion { get; set; }

    public string? StandardErhalteneInformationen { get; set; }

    public bool StandardWahrheitsserum { get; set; }

    public MassnahmeAusgang StandardAusgang { get; set; } = MassnahmeAusgang.LaeuftNoch;

    // ---- IAuditable ----
    public DateTime ErstelltAm { get; set; }
    public string? ErstelltVonId { get; set; }
    public DateTime? GeaendertAm { get; set; }
    public string? GeaendertVonId { get; set; }

    // ---- ISoftDelete ----
    public bool IstGeloescht { get; set; }
    public DateTime? GeloeschtAm { get; set; }
    public string? GeloeschtVonId { get; set; }
}
