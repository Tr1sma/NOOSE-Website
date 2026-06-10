using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Data.Entities.Parteien;

/// <summary>
/// Eine Partei (politische Organisation) als vollwertige Akte – Phase 5a. Bündelt Stammdaten, ihre
/// Mitglieder (Personen, mit Rolle/Leitung) und zugeteilte Special Agents sowie eine Einstufung mit
/// Verlauf. Die Leitung ist kein eigenes Feld, sondern wird über das Mitglieds-Flag <c>IstLeitung</c>
/// abgebildet (Parität zu Fraktion/Gruppe). Voll auditiert und papierkorbfähig (<see cref="IAuditable"/>
/// + <see cref="ISoftDelete"/>). Konflikte/Bündnisse zu anderen Organisationen laufen über die generische
/// Verknüpfungs-Engine.
/// </summary>
public class Partei : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Menschenlesbares, eindeutiges Aktenzeichen (z. B. NOOSE-PT-2026-0001).</summary>
    public string Aktenzeichen { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Beschreibung { get; set; }
    public string? Ziele { get; set; }

    /// <summary>Interne Bemerkungen/Vermerke zur Partei (Freitext, getrennt von Beschreibung/Zielen).</summary>
    public string? Bemerkungen { get; set; }

    public Einstufung Einstufung { get; set; } = Einstufung.Unbekannt;

    /// <summary>Verschlusssache: in Liste/Detail nur für Führung/Admin sichtbar.</summary>
    public bool IstVerschlusssache { get; set; }

    // ---- Kind-Tabellen ----
    public List<ParteiMitglied> Mitglieder { get; set; } = new();
    public List<ParteiAgent> Agenten { get; set; } = new();

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
