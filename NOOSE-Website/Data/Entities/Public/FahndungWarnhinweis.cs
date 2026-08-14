using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Public;

/// <summary>Assigns one warning chip to one public wanted notice.</summary>
/// <remarks>
/// Typed rather than polymorphic like <see cref="Common.TagMapping"/>: both ends are known, so the FKs can do the
/// cleaning. Neither auditable nor soft-deletable — the diff is logged against the notice via ManualAudit, because a
/// change here changes what the public sees.
/// </remarks>
[Table("FahndungWarnhinweise")]
public class FahndungWarnhinweis
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("FahndungId")]
    public string FahndungId { get; set; } = string.Empty;
    public OeffentlicheFahndung? Fahndung { get; set; }

    [Column("WarnhinweisId")]
    public string WarnhinweisId { get; set; } = string.Empty;
    public Warnhinweis? Warnhinweis { get; set; }
}
