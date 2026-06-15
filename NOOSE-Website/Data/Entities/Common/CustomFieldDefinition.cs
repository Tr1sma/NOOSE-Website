using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>
/// Admin-definiertes Zusatzfeld („Custom-Feld") für einen Aktentyp – konfigurierbar ohne Code
/// (Plan.md Phase 7). Legt Name, Typ und Optionen fest; die konkreten Werte je Akte liegen in
/// <see cref="CustomFeldWert"/> (polymorph über <see cref="CustomFeldWert.EntitaetTyp"/> +
/// <see cref="CustomFeldWert.EntitaetId"/>). Voll auditiert und papierkorbfähig.
/// </summary>
[Table("CustomFeldDefinitionen")]
public class CustomFieldDefinition : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Aktentyp, für den das Feld gilt, z. B. <c>nameof(Person)</c> (CLR-Typname).</summary>
    [Column("EntitaetTyp")]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Feld-Bezeichnung (Label), z. B. „Decknamen-Quelle".</summary>
    public string Name { get; set; } = string.Empty;

    [Column("FeldTyp")]
    public CustomFieldType FieldType { get; set; }

    /// <summary>Auswahl-Optionen bei <see cref="CustomFeldTyp.Auswahl"/> – eine Option pro Zeile.</summary>
    [Column("Optionen")]
    public string? Options { get; set; }

    /// <summary>Pflichtfeld: beim Speichern der Werte muss ein Wert gesetzt sein.</summary>
    [Column("Pflicht")]
    public bool Mandatory { get; set; }

    /// <summary>Sortierreihenfolge im Panel (kleiner zuerst).</summary>
    [Column("Reihenfolge")]
    public int Order { get; set; }

    /// <summary>Nur aktive Felder erscheinen im Zusatzfelder-Panel der Akten.</summary>
    [Column("IstAktiv")]
    public bool IsActive { get; set; } = true;

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
