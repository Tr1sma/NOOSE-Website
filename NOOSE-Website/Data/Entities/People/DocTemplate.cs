using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.People;

/// <summary>
/// Admin-/Führungs-definierte Erfassungsmaske („Vorlage") für ein Personen-Dok. Hält Default-Werte für
/// die Dok-Felder, mit denen das Anlege-Formular vorbefüllt wird (frei editierbar). Setzt die „Vorgaben"
/// um (Plan.md Phase 7). Voll auditiert und papierkorbfähig.
/// </summary>
[Table("DokVorlagen")]
public class DocTemplate : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Sprechender Name der Vorlage, z. B. „Verhör – Standard". Eindeutig.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optionale Erläuterung – wird in der Verwaltung und im Vorlagen-Picker angezeigt.</summary>
    [Column("Beschreibung")]
    public string? Description { get; set; }

    /// <summary>Nur aktive Vorlagen erscheinen im Picker beim Dok-Anlegen.</summary>
    [Column("IstAktiv")]
    public bool IsActive { get; set; } = true;

    /// <summary>Sortierreihenfolge im Picker/der Liste (kleiner zuerst).</summary>
    [Column("Sortierung")]
    public int Sorting { get; set; }

    // ---- Default-Werte für die Dok-Felder (gespiegelte editierbare Teilmenge von PersonDok) ----
    [Column("StandardGrund")]
    public string? DefaultReason { get; set; }

    /// <summary>Default für die Fraktionszugehörigkeit als Freitext (Org-Verknüpfung ist instanzspezifisch).</summary>
    [Column("StandardFraktion")]
    public string? DefaultFaction { get; set; }

    [Column("StandardErhalteneInformationen")]
    public string? DefaultReceivedInformation { get; set; }

    [Column("StandardWahrheitsserum")]
    public bool DefaultTruthSerum { get; set; }

    [Column("StandardAusgang")]
    public MeasureOutcome DefaultOutcome { get; set; } = MeasureOutcome.RunningStill;

    // ---- IAuditable ----
    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }

    // ---- ISoftDelete ----
    [Column("IstGeloescht")]
    public bool IsDeleted { get; set; }
    [Column("GeloeschtAm")]
    public DateTime? DeletedAt { get; set; }
    [Column("GeloeschtVonId")]
    public string? DeletedById { get; set; }
}
