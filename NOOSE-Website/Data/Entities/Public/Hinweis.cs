using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Public;

/// <summary>A tip submitted by a citizen, optionally about a published wanted notice.</summary>
/// <remarks>
/// The case number is minted on submission, not on some later publication: a tip exists from its first second and the
/// citizen needs a handle for it right away. Prefix <c>H</c>.
/// <para>
/// <see cref="WantsAnonymity"/> is a promise about the handler's view, not about storage — the account stays on the
/// row for abuse control and for a later reward, and only leadership may resolve it.
/// </para>
/// </remarks>
[Table("Hinweise")]
public class Hinweis : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("Aktenzeichen")]
    public string CaseNumber { get; set; } = string.Empty;

    [Column("BuergerProfilId")]
    public string CitizenProfileId { get; set; } = string.Empty;
    public BuergerProfil? CitizenProfile { get; set; }

    [Column("AnonymGewuenscht")]
    public bool WantsAnonymity { get; set; }

    /// <summary>Notice the tip refers to; resolved from a published case number, never taken as an id from outside.</summary>
    [Column("FahndungId")]
    public string? WantedId { get; set; }
    public OeffentlicheFahndung? Wanted { get; set; }

    [Column("Text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>Server-assigned name under the tips upload path; never the name the citizen sent.</summary>
    [Column("AnhangDateiname")]
    public string? AttachmentFileName { get; set; }

    [Column("AnhangOriginalname")]
    public string? AttachmentOriginalName { get; set; }

    [Column("AnhangTyp")]
    public string? AttachmentContentType { get; set; }

    [Column("Status")]
    public TipStatus Status { get; set; } = TipStatus.Neu;

    [Column("BearbeiterId")]
    public string? HandlerId { get; set; }
    public Agent? Handler { get; set; }

    /// <summary>Column now, grouping logic with the duplicate detection phase.</summary>
    [Column("DublettenGruppeId")]
    public string? DuplicateGroupId { get; set; }

    /// <summary>Effective order of the inbox; computed unless an override below pins it.</summary>
    [Column("Prioritaet")]
    public int Priority { get; set; }

    /// <summary>Priority set by hand; while it is present the automatic recomputation leaves the row alone.</summary>
    [Column("PrioritaetManuell")]
    public int? PriorityOverride { get; set; }

    [Column("PrioritaetManuellGrund")]
    public string? PriorityOverrideReason { get; set; }

    [Column("AnonymitaetAufgeloestAm")]
    public DateTime? AnonymityResolvedAt { get; set; }
    [Column("AnonymitaetAufgeloestVonId")]
    public string? AnonymityResolvedById { get; set; }

    /// <summary>Drives the citizen's unread count; only the citizen's own reading moves it.</summary>
    [Column("ZuletztGelesenBuergerAm")]
    public DateTime? CitizenLastReadAt { get; set; }

    /// <summary>Drives the inbox badge: set the first time any agent opens the tip.</summary>
    /// <remarks>
    /// One stamp for the whole desk, not one per agent: the badge answers "has anyone looked at this yet", which is
    /// what the inbox is for. A per-agent read state would need its own table and would mean five agents each have
    /// to open every tip before the number goes away.
    /// </remarks>
    [Column("ZuletztGelesenAgentAm")]
    public DateTime? AgentLastReadAt { get; set; }

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
