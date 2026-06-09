using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Data.Entities.Gruppen;

/// <summary>
/// Eine Personengruppe (loser Zusammenschluss von Personen) als Akte – Phase 4. Bündelt Mitglieder,
/// zugeteilte Agents und eine Einstufung mit Verlauf. Der Erfassungsfortschritt „x/y" ergibt sich aus
/// den erfassten Mitgliedern (x) gegenüber der geschätzten Gesamtgröße (y, <see cref="GeschaetzteMitgliederzahl"/>).
/// Voll auditiert und papierkorbfähig (<see cref="IAuditable"/> + <see cref="ISoftDelete"/>).
/// </summary>
public class Personengruppe : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Menschenlesbares, eindeutiges Aktenzeichen (z. B. NOOSE-G-2026-0001).</summary>
    public string Aktenzeichen { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Beschreibung { get; set; }
    public string? Ziele { get; set; }

    public Einstufung Einstufung { get; set; } = Einstufung.Unbekannt;

    /// <summary>Geschätzte Gesamtgröße der Gruppe (= y im Erfassungsfortschritt x/y); optional.</summary>
    public int? GeschaetzteMitgliederzahl { get; set; }

    /// <summary>Verschlusssache: in Liste/Detail nur für Führung/Admin sichtbar.</summary>
    public bool IstVerschlusssache { get; set; }

    // ---- Kind-Tabellen ----
    public List<PersonengruppeMitglied> Mitglieder { get; set; } = new();
    public List<PersonengruppeAgent> Agenten { get; set; } = new();

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
