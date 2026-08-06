using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Kasse;

/// <summary>A deposit, withdrawal or balance-correction booked against a NOOSE cash account; the balance is computed from the ledger, never stored.</summary>
[Table("KassenBuchungen")]
public class KassenBuchung : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("Aktenzeichen")]
    public string CaseNumber { get; set; } = string.Empty;

    [Column("Konto")]
    public KassenKonto Account { get; set; }

    [Column("Art")]
    public KassenBuchungArt Kind { get; set; }

    /// <summary>Magnitude for a deposit/withdrawal; the target balance for a correction.</summary>
    [Column("Betrag")]
    public decimal Amount { get; set; }

    [Column("Verwendungszweck")]
    public string? Reason { get; set; }

    /// <summary>Agent who booked the entry.</summary>
    [Column("BuchungVonId")]
    public string? BookedById { get; set; }
    public Agent? BookedBy { get; set; }

    /// <summary>When the booking happened (stored UTC); drives the chronological ledger order.</summary>
    [Column("Zeitpunkt")]
    public DateTime Timestamp { get; set; }

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
