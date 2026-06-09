using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Data.Entities.Querschnitt;

/// <summary>
/// Eine gerichtet gespeicherte, aber <b>bidirektional</b> dargestellte Verknüpfung zwischen zwei
/// beliebigen Akten (polymorph über Von-/Nach-Typ + -Id). Wird nur einmal abgelegt; der Dienst
/// normalisiert beim Laden auf „die jeweils andere Seite". Speist später Beziehungsgraph/Pfadsuche.
/// </summary>
public class Verknuepfung : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string VonTyp { get; set; } = string.Empty;
    public string VonId { get; set; } = string.Empty;

    public string NachTyp { get; set; } = string.Empty;
    public string NachId { get; set; } = string.Empty;

    /// <summary>Optionaler Beziehungslabel/Notiz („Quelle für", „Revierkonflikt", „Waffenbruderschaft" …).</summary>
    public string? Label { get; set; }

    /// <summary>Art der Verknüpfung: allgemein (Standard) oder fachliche Organisations-Beziehung (Konflikt/Bündnis).</summary>
    public VerknuepfungArt Art { get; set; } = VerknuepfungArt.Standard;

    /// <summary>
    /// Automatisch erzeugte Verknüpfung (z. B. „Fraktionskollege" durch eine Fraktions-Mitgliedschaft).
    /// Wird vom System gepflegt – nicht manuell anlegbar/löschbar – und bei Wegfall der Grundlage entfernt.
    /// </summary>
    public bool Automatisch { get; set; }

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
