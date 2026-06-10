using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Data.Entities.Vorgaenge;

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
public class Vorgang : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Menschenlesbares, eindeutiges Aktenzeichen (z. B. NOOSE-V-2026-0001).</summary>
    public string Aktenzeichen { get; set; } = string.Empty;

    public string Titel { get; set; } = string.Empty;

    /// <summary>Typ/Kategorie des Vorgangs (z. B. Ermittlung, Überwachung) – Freitext mit Vorschlägen.</summary>
    public string? Typ { get; set; }

    public VorgangStatus Status { get; set; } = VorgangStatus.Offen;

    /// <summary>Sachverhalt/Worum geht es (Freitext).</summary>
    public string? Beschreibung { get; set; }

    /// <summary>Aktueller Stand/Zusammenfassung des Vorgangs (Freitext).</summary>
    public string? Zusammenfassung { get; set; }

    /// <summary>Abschlussvermerk (Freitext, beim Schließen des Vorgangs).</summary>
    public string? Abschlussvermerk { get; set; }

    /// <summary>Zeitpunkt des Abschlusses – wird gesetzt, sobald der Status auf Abgeschlossen/Archiviert wechselt.</summary>
    public DateTime? AbgeschlossenAm { get; set; }

    public Einstufung Einstufung { get; set; } = Einstufung.Unbekannt;

    /// <summary>Verschlusssache: in Liste/Detail nur für Führung/Admin sichtbar.</summary>
    public bool IstVerschlusssache { get; set; }

    // ---- Kind-Tabellen ----
    public List<VorgangAgent> Agenten { get; set; } = new();

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
