using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Ankuendigungen;

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
public class Ankuendigung : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Menschenlesbares, eindeutiges Aktenzeichen (z. B. NOOSE-N-2026-0001).</summary>
    [Column("Aktenzeichen")]
    public string Aktenzeichen { get; set; } = string.Empty;

    [Column("Titel")]
    public string Titel { get; set; } = string.Empty;

    /// <summary>Fließtext der Ankündigung (mit optionalen @-Erwähnungen). Kann leer sein (reine Titel-Notiz).</summary>
    [Column("Inhalt")]
    public string Inhalt { get; set; } = string.Empty;

    /// <summary>Hervorgehoben/angepinnt – erscheint oben am Brett.</summary>
    [Column("Wichtig")]
    public bool Wichtig { get; set; }

    [Column("Zielgruppe")]
    public AnkuendigungZielgruppe Zielgruppe { get; set; } = AnkuendigungZielgruppe.AlleAktiven;

    /// <summary>Bei <see cref="AnkuendigungZielgruppe.Taskforce"/>: die Taskforce-Id; sonst null.</summary>
    [Column("ZielId")]
    public string? ZielId { get; set; }

    /// <summary>Bei <see cref="AnkuendigungZielgruppe.AbDienstgrad"/>: der Mindest-Dienstgrad; sonst null.</summary>
    [Column("MinDienstgrad")]
    public Dienstgrad? MinDienstgrad { get; set; }

    /// <summary>True = zusätzlich als Glocken-Broadcast an den Empfängerkreis gepusht (Führung).</summary>
    [Column("AlsBroadcast")]
    public bool AlsBroadcast { get; set; }

    /// <summary>True = der Empfängerkreis muss die Ankündigung quittieren (Lesebestätigung; Führung).</summary>
    [Column("QuittierungVerlangt")]
    public bool QuittierungVerlangt { get; set; }

    // ---- Kind-Tabellen ----
    /// <summary>Empfänger-Snapshot für die Quittierung – nur befüllt, wenn <see cref="QuittierungVerlangt"/>.</summary>
    public List<AnkuendigungQuittierung> Quittierungen { get; set; } = new();

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
