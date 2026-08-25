using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Public;

/// <summary>A citizen's objection to one public wanted notice. Prefix <c>EIN</c>.</summary>
/// <remarks>
/// Hangs off the notice, not off the file: the objection disputes what the agency published, and the notice is the
/// only thing the citizen ever saw. The timeline reaches the file through the notice, one hop further than a bounty
/// share.
/// <para>
/// There is no message thread. The agency answers once, in <see cref="DecisionNote"/>, and the citizen reads it with
/// the status — anything longer belongs in a ticket.
/// </para>
/// </remarks>
[Table("FahndungEinsprueche")]
public class FahndungEinspruch : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("Aktenzeichen")]
    public string CaseNumber { get; set; } = string.Empty;

    [Column("FahndungId")]
    public string WantedId { get; set; } = string.Empty;
    public OeffentlicheFahndung? Wanted { get; set; }

    [Column("BuergerProfilId")]
    public string CitizenProfileId { get; set; } = string.Empty;
    public BuergerProfil? CitizenProfile { get; set; }

    /// <summary>Plain text, like every other citizen submission: nothing to sanitize, nothing to forget.</summary>
    [Column("Text")]
    public string Text { get; set; } = string.Empty;

    [Column("Status")]
    public ObjectionStatus Status { get; set; } = ObjectionStatus.Neu;

    /// <summary>The one answer the citizen gets; written with the decision and readable by them.</summary>
    [Column("Entscheidungsnotiz")]
    public string? DecisionNote { get; set; }

    [Column("EntschiedenVonId")]
    public string? DecidedById { get; set; }
    public Agent? DecidedBy { get; set; }

    [Column("EntschiedenAm")]
    public DateTime? DecidedAt { get; set; }

    /// <summary>Case opened over this objection; internal, and never part of what the citizen reads.</summary>
    [Column("VorgangId")]
    public string? LinkedCaseId { get; set; }
    public Case? LinkedCase { get; set; }

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
