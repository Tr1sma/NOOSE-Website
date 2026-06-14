using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Querschnitt;

/// <summary>
/// Eine gerichtet gespeicherte, aber <b>bidirektional</b> dargestellte Verknüpfung zwischen zwei
/// beliebigen Akten (polymorph über Von-/Nach-Typ + -Id). Wird nur einmal abgelegt; der Dienst
/// normalisiert beim Laden auf „die jeweils andere Seite". Speist später Beziehungsgraph/Pfadsuche.
/// </summary>
[Table("Verknuepfungen")]
public class Verknuepfung : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("VonTyp")]
    public string VonTyp { get; set; } = string.Empty;
    [Column("VonId")]
    public string VonId { get; set; } = string.Empty;

    [Column("NachTyp")]
    public string NachTyp { get; set; } = string.Empty;
    [Column("NachId")]
    public string NachId { get; set; } = string.Empty;

    /// <summary>Optionaler Beziehungslabel/Notiz („Quelle für", „Revierkonflikt", „Waffenbruderschaft" …).</summary>
    public string? Label { get; set; }

    /// <summary>Art der Verknüpfung: allgemein (Standard) oder fachliche Organisations-Beziehung (Konflikt/Bündnis).</summary>
    [Column("Art")]
    public VerknuepfungArt Art { get; set; } = VerknuepfungArt.Standard;

    /// <summary>
    /// Automatisch erzeugte Verknüpfung (z. B. „Fraktionskollege" durch eine Fraktions-Mitgliedschaft).
    /// Wird vom System gepflegt – nicht manuell anlegbar/löschbar – und bei Wegfall der Grundlage entfernt.
    /// </summary>
    [Column("Automatisch")]
    public bool Automatisch { get; set; }

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
