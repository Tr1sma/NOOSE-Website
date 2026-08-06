using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Kasse;

/// <summary>A reusable booking preset (account, kind, amount, reason) applied with one confirm; leadership-managed.</summary>
[Table("KassenVorlagen")]
public class KassenBuchungVorlage : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Template name; unique (service-checked).</summary>
    public string Name { get; set; } = string.Empty;

    [Column("Konto")]
    public KassenKonto Account { get; set; }

    [Column("Art")]
    public KassenBuchungArt Kind { get; set; }

    [Column("Betrag")]
    public decimal Amount { get; set; }

    [Column("Verwendungszweck")]
    public string? Reason { get; set; }

    /// <summary>Only active templates appear in the quick-book bar.</summary>
    [Column("IstAktiv")]
    public bool IsActive { get; set; } = true;

    [Column("Sortierung")]
    public int Sorting { get; set; }

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }

    [Column("IstGeloescht")]
    public bool IsDeleted { get; set; }
    [Column("GeloeschtAm")]
    public DateTime? DeletedAt { get; set; }
    [Column("GeloeschtVonId")]
    public string? DeletedById { get; set; }
}
