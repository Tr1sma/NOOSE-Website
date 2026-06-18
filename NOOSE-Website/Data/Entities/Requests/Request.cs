using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Requests;

/// <summary>
/// Ein Antrag im Posteingang-Workflow (Phase 5). Generisch über <see cref="AntragTyp"/> und ein
/// polymorphes Ziel (<see cref="ZielTyp"/> = CLR-Typname der Akte + <see cref="ZielId"/>). Aktuell genutzt
/// für die <see cref="AntragTyp.Hochstufung"/>: ein Agent unterhalb Senior Special Agent beantragt die
/// Einstufung „Gesichert staatsgefährdend" einer Akte; ein Senior Special Agent+ entscheidet im Posteingang.
/// Bei Genehmigung wird die Einstufung der Ziel-Akte gesetzt und im polymorphen Einstufungs-Verlauf mit
/// Antrags-Bezug (<c>EinstufungVerlauf.AntragId</c>) protokolliert.
/// </summary>
[Table("Antraege")]
public class Request : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("Typ")]
    public RequestType Type { get; set; } = RequestType.Upgrade;

    /// <summary>CLR-Typname der Ziel-Akte (z. B. <c>nameof(Person)</c>, <c>nameof(Fraktion)</c>).</summary>
    [Column("ZielTyp")]
    public string TargetType { get; set; } = string.Empty;

    /// <summary>Id der Ziel-Akte.</summary>
    [Column("ZielId")]
    public string TargetId { get; set; } = string.Empty;

    /// <summary>Denormalisierte Anzeige der Ziel-Akte (Name + Aktenzeichen) für den Posteingang.</summary>
    [Column("ZielBezeichnung")]
    public string TargetDesignation { get; set; } = string.Empty;

    /// <summary>Gewünschte Einstufung (für die Hochstufung stets „Gesichert staatsgefährdend").</summary>
    [Column("ZielEinstufung")]
    public Classification TargetClassification { get; set; }

    /// <summary>Begründung des Antragstellers.</summary>
    [Column("Begruendung")]
    public string? Justification { get; set; }

    public RequestStatus Status { get; set; } = RequestStatus.Requested;

    /// <summary>Codename des Antragstellers (denormalisiert).</summary>
    [Column("AntragstellerName")]
    public string? RequesterName { get; set; }

    /// <summary>Codename des Entscheiders (denormalisiert), null solange offen.</summary>
    [Column("EntscheiderName")]
    public string? DeciderName { get; set; }

    [Column("EntschiedenAm")]
    public DateTime? DecidedAt { get; set; }

    /// <summary>Optionale Notiz/Begründung der Entscheidung.</summary>
    [Column("Entscheidungsnotiz")]
    public string? DecisionNote { get; set; }

    // ---- Partner-Freigabe fields (only set when Type == PartnerFreigabe) ----
    [Column("FreigabeBehoerde")]
    public PartnerAgency? FreigabeAgency { get; set; }

    [Column("FreigabePartnerAgentId")]
    public string? FreigabePartnerAgentId { get; set; }

    [Column("FreigabeInklusiveKinder")]
    public bool FreigabeIncludesChildren { get; set; }

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
