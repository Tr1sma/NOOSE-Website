using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Announcements;

/// <summary>
/// Eine Ankündigung am Schwarzen Brett – Phase 6. Vereint „News/Schwarzes Brett" und „Behörden-Broadcast":
/// erscheint für ihre <see cref="Zielgruppe"/> am Brett; optional zusätzlich als Broadcast in die Glocke gepusht
/// (<see cref="AlsBroadcast"/>) und optional mit Lesebestätigung (<see cref="QuittierungVerlangt"/>). Ein einfacher
/// Brett-Eintrag (Zielgruppe Alle, kein Push, keine Quittierung) darf von jedem aktiven Agenten erstellt werden;
/// die Broadcast-Features sind der Führung vorbehalten. <b>Kein</b> verlinkbarer Akten-Typ – nur eine Mitteilung;
/// der <see cref="Inhalt"/> trägt jedoch <c>@{Typ:Id}</c>-Erwähnungstokens (Auflösung beim Anzeigen). Voll auditiert
/// und papierkorbfähig (<see cref="IAuditable"/> + <see cref="ISoftDelete"/>). <c>ErstelltVonId</c> ist der Verfasser.
/// </summary>
[Table("Ankuendigungen")]
public class Announcement : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Menschenlesbares, eindeutiges Aktenzeichen (z. B. NOOSE-N-2026-0001).</summary>
    [Column("Aktenzeichen")]
    public string CaseNumber { get; set; } = string.Empty;

    [Column("Titel")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Fließtext der Ankündigung (mit optionalen @-Erwähnungen). Kann leer sein (reine Titel-Notiz).</summary>
    [Column("Inhalt")]
    public string Content { get; set; } = string.Empty;

    /// <summary>Hervorgehoben/angepinnt – erscheint oben am Brett.</summary>
    [Column("Wichtig")]
    public bool Important { get; set; }

    [Column("Zielgruppe")]
    public AnnouncementAudience Audience { get; set; } = AnnouncementAudience.AllActive;

    /// <summary>Bei <see cref="AnkuendigungZielgruppe.Taskforce"/>: die Taskforce-Id; sonst null.</summary>
    [Column("ZielId")]
    public string? TargetId { get; set; }

    /// <summary>Bei <see cref="AnkuendigungZielgruppe.AbDienstgrad"/>: der Mindest-Dienstgrad; sonst null.</summary>
    [Column("MinDienstgrad")]
    public Rank? MinRank { get; set; }

    /// <summary>True = zusätzlich als Glocken-Broadcast an den Empfängerkreis gepusht (Führung).</summary>
    [Column("AlsBroadcast")]
    public bool AsBroadcast { get; set; }

    /// <summary>True = der Empfängerkreis muss die Ankündigung quittieren (Lesebestätigung; Führung).</summary>
    [Column("QuittierungVerlangt")]
    public bool AcknowledgmentRequired { get; set; }

    // ---- Kind-Tabellen ----
    /// <summary>Empfänger-Snapshot für die Quittierung – nur befüllt, wenn <see cref="QuittierungVerlangt"/>.</summary>
    public List<AnnouncementAcknowledgment> Acknowledgments { get; set; } = new();

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
