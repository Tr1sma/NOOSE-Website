using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Cases;

/// <summary>
/// Eine Vorgangs-/Fallakte als <b>übergeordnete</b> Akte – Phase 5. Bündelt mehrere Einzelakten
/// (Personen, Operationen, Observationen, einzelne <see cref="Personen.PersonDok"/>, Fraktionen,
/// Personengruppen, Parteien, Taskforces) zu einem Ermittlungs-Vorgang mit eigenem
/// <see cref="VorgangStatus"/>. Die gebündelten Mitglieder hängen <b>nicht</b> an eigenen Join-Tabellen,
/// sondern laufen über die generische Verknüpfungs-Engine (<c>Verknuepfung</c>) – exakt wie die Beteiligten
/// einer Operation. Beteiligte Agents (mit Fallführer-Markierung) laufen über <see cref="VorgangAgent"/>.
/// Trägt eine Einstufung mit Verlauf, ist voll auditiert und papierkorbfähig
/// (<see cref="IAuditable"/> + <see cref="ISoftDelete"/>).
/// </summary>
[Table("Vorgaenge")]
public class Case : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Menschenlesbares, eindeutiges Aktenzeichen (z. B. NOOSE-V-2026-0001).</summary>
    [Column("Aktenzeichen")]
    public string CaseNumber { get; set; } = string.Empty;

    [Column("Titel")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Typ/Kategorie des Vorgangs (z. B. Ermittlung, Überwachung) – Freitext mit Vorschlägen.</summary>
    [Column("Typ")]
    public string? Type { get; set; }

    public CaseStatus Status { get; set; } = CaseStatus.Open;

    /// <summary>Sachverhalt/Worum geht es (Freitext).</summary>
    [Column("Beschreibung")]
    public string? Description { get; set; }

    /// <summary>Aktueller Stand/Zusammenfassung des Vorgangs (Freitext).</summary>
    [Column("Zusammenfassung")]
    public string? Summary { get; set; }

    /// <summary>Abschlussvermerk (Freitext, beim Schließen des Vorgangs).</summary>
    [Column("Abschlussvermerk")]
    public string? ClosingNote { get; set; }

    /// <summary>Zeitpunkt des Abschlusses – wird gesetzt, sobald der Status auf Abgeschlossen/Archiviert wechselt.</summary>
    [Column("AbgeschlossenAm")]
    public DateTime? CompletedAt { get; set; }

    [Column("Einstufung")]
    public Classification Classification { get; set; } = Classification.Unknown;

    /// <summary>Verschlusssache: in Liste/Detail nur für Führung/Admin sichtbar.</summary>
    [Column("IstVerschlusssache")]
    public bool IsClassified { get; set; }

    // ---- Kind-Tabellen ----
    public List<CaseAgent> Agents { get; set; } = new();

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
