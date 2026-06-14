using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Personen;

/// <summary>
/// Admin-/Führungs-definierte Erfassungsmaske („Vorlage") für ein Personen-Dok. Hält Default-Werte für
/// die Dok-Felder, mit denen das Anlege-Formular vorbefüllt wird (frei editierbar). Setzt die „Vorgaben"
/// um (Plan.md Phase 7). Voll auditiert und papierkorbfähig.
/// </summary>
[Table("DokVorlagen")]
public class DokVorlage : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Sprechender Name der Vorlage, z. B. „Verhör – Standard". Eindeutig.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optionale Erläuterung – wird in der Verwaltung und im Vorlagen-Picker angezeigt.</summary>
    [Column("Beschreibung")]
    public string? Beschreibung { get; set; }

    /// <summary>Nur aktive Vorlagen erscheinen im Picker beim Dok-Anlegen.</summary>
    [Column("IstAktiv")]
    public bool IstAktiv { get; set; } = true;

    /// <summary>Sortierreihenfolge im Picker/der Liste (kleiner zuerst).</summary>
    [Column("Sortierung")]
    public int Sortierung { get; set; }

    // ---- Default-Werte für die Dok-Felder (gespiegelte editierbare Teilmenge von PersonDok) ----
    [Column("StandardGrund")]
    public string? StandardGrund { get; set; }

    /// <summary>Default für die Fraktionszugehörigkeit als Freitext (Org-Verknüpfung ist instanzspezifisch).</summary>
    [Column("StandardFraktion")]
    public string? StandardFraktion { get; set; }

    [Column("StandardErhalteneInformationen")]
    public string? StandardErhalteneInformationen { get; set; }

    [Column("StandardWahrheitsserum")]
    public bool StandardWahrheitsserum { get; set; }

    [Column("StandardAusgang")]
    public MassnahmeAusgang StandardAusgang { get; set; } = MassnahmeAusgang.LaeuftNoch;

    // ---- IAuditable ----
    [Column("ErstelltAm")]
    public DateTime ErstelltAm { get; set; }
    [Column("ErstelltVonId")]
    public string? ErstelltVonId { get; set; }
    [Column("GeaendertAm")]
    public DateTime? GeaendertAm { get; set; }
    [Column("GeaendertVonId")]
    public string? GeaendertVonId { get; set; }

    // ---- ISoftDelete ----
    [Column("IstGeloescht")]
    public bool IstGeloescht { get; set; }
    [Column("GeloeschtAm")]
    public DateTime? GeloeschtAm { get; set; }
    [Column("GeloeschtVonId")]
    public string? GeloeschtVonId { get; set; }
}
