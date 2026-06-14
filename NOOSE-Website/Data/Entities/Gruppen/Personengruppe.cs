using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Gruppen;

/// <summary>
/// Eine Personengruppe (loser Zusammenschluss von Personen) als Akte – Phase 4. Bündelt Mitglieder,
/// zugeteilte Agents und eine Einstufung mit Verlauf. Der Erfassungsfortschritt „x/y" ergibt sich aus
/// den erfassten Mitgliedern (x) gegenüber der geschätzten Gesamtgröße (y, <see cref="GeschaetzteMitgliederzahl"/>).
/// Voll auditiert und papierkorbfähig (<see cref="IAuditable"/> + <see cref="ISoftDelete"/>).
/// </summary>
[Table("Personengruppen")]
public class Personengruppe : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Menschenlesbares, eindeutiges Aktenzeichen (z. B. NOOSE-G-2026-0001).</summary>
    [Column("Aktenzeichen")]
    public string Aktenzeichen { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    [Column("Beschreibung")]
    public string? Beschreibung { get; set; }
    [Column("Ziele")]
    public string? Ziele { get; set; }

    /// <summary>Kategorie der Gruppen-Akte (Persönlichkeit/Gruppierung/Person of Interest); Default Gruppierung.</summary>
    [Column("Art")]
    public GruppenArt Art { get; set; } = GruppenArt.Gruppierung;

    [Column("Einstufung")]
    public Einstufung Einstufung { get; set; } = Einstufung.Unbekannt;

    /// <summary>Geschätzte Gesamtgröße der Gruppe (= y im Erfassungsfortschritt x/y); optional.</summary>
    [Column("GeschaetzteMitgliederzahl")]
    public int? GeschaetzteMitgliederzahl { get; set; }

    /// <summary>Verschlusssache: in Liste/Detail nur für Führung/Admin sichtbar.</summary>
    [Column("IstVerschlusssache")]
    public bool IstVerschlusssache { get; set; }

    // ---- Kind-Tabellen ----
    public List<PersonengruppeMitglied> Mitglieder { get; set; } = new();
    public List<PersonengruppeAgent> Agenten { get; set; } = new();

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
