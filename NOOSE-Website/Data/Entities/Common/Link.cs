using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>
/// Eine gerichtet gespeicherte, aber <b>bidirektional</b> dargestellte Verknüpfung zwischen zwei
/// beliebigen Akten (polymorph über Von-/Nach-Typ + -Id). Wird nur einmal abgelegt; der Dienst
/// normalisiert beim Laden auf „die jeweils andere Seite". Speist später Beziehungsgraph/Pfadsuche.
/// </summary>
[Table("Verknuepfungen")]
public class Link : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("VonTyp")]
    public string SourceType { get; set; } = string.Empty;
    [Column("VonId")]
    public string SourceId { get; set; } = string.Empty;

    [Column("NachTyp")]
    public string TargetType { get; set; } = string.Empty;
    [Column("NachId")]
    public string TargetId { get; set; } = string.Empty;

    /// <summary>Optionaler Beziehungslabel/Notiz („Quelle für", „Revierkonflikt", „Waffenbruderschaft" …).</summary>
    public string? Label { get; set; }

    /// <summary>Art der Verknüpfung: allgemein (Standard) oder fachliche Organisations-Beziehung (Konflikt/Bündnis).</summary>
    [Column("Art")]
    public LinkKind Kind { get; set; } = LinkKind.Default;

    /// <summary>
    /// Automatisch erzeugte Verknüpfung (z. B. „Fraktionskollege" durch eine Fraktions-Mitgliedschaft).
    /// Wird vom System gepflegt – nicht manuell anlegbar/löschbar – und bei Wegfall der Grundlage entfernt.
    /// </summary>
    [Column("Automatisch")]
    public bool Automatic { get; set; }

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
