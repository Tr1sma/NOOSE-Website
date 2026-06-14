using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Querschnitt;

/// <summary>
/// Admin-definiertes Zusatzfeld („Custom-Feld") für einen Aktentyp – konfigurierbar ohne Code
/// (Plan.md Phase 7). Legt Name, Typ und Optionen fest; die konkreten Werte je Akte liegen in
/// <see cref="CustomFeldWert"/> (polymorph über <see cref="CustomFeldWert.EntitaetTyp"/> +
/// <see cref="CustomFeldWert.EntitaetId"/>). Voll auditiert und papierkorbfähig.
/// </summary>
[Table("CustomFeldDefinitionen")]
public class CustomFeldDefinition : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Aktentyp, für den das Feld gilt, z. B. <c>nameof(Person)</c> (CLR-Typname).</summary>
    [Column("EntitaetTyp")]
    public string EntitaetTyp { get; set; } = string.Empty;

    /// <summary>Feld-Bezeichnung (Label), z. B. „Decknamen-Quelle".</summary>
    public string Name { get; set; } = string.Empty;

    [Column("FeldTyp")]
    public CustomFeldTyp FeldTyp { get; set; }

    /// <summary>Auswahl-Optionen bei <see cref="CustomFeldTyp.Auswahl"/> – eine Option pro Zeile.</summary>
    [Column("Optionen")]
    public string? Optionen { get; set; }

    /// <summary>Pflichtfeld: beim Speichern der Werte muss ein Wert gesetzt sein.</summary>
    [Column("Pflicht")]
    public bool Pflicht { get; set; }

    /// <summary>Sortierreihenfolge im Panel (kleiner zuerst).</summary>
    [Column("Reihenfolge")]
    public int Reihenfolge { get; set; }

    /// <summary>Nur aktive Felder erscheinen im Zusatzfelder-Panel der Akten.</summary>
    [Column("IstAktiv")]
    public bool IstAktiv { get; set; } = true;

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
