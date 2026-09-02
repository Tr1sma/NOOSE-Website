using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Public;

/// <summary>What the agency says about the situation: a level, an assessment, and since when.</summary>
/// <remarks>
/// Outward. Structurally carries no agent, no record, no id and no number out of the scoring system — the trend is
/// the level that stood before this one, which is a statement the agency made rather than a figure computed over the
/// stock. There is deliberately no "unset" level: the read path answers with null when nothing has been said.
/// </remarks>
/// <param name="Note">Plain text. Line breaks are its formatting; it is never HTML and never rendered as markup.</param>
/// <param name="Since">When the current level was set. A corrected assessment does not move it.</param>
/// <param name="Previous">The level before this one; null while the first level still stands.</param>
public sealed record PublicSituationState(
    PublicSituationLevel Level,
    string Note,
    DateTime? Since,
    PublicSituationLevel? Previous);

/// <summary>What the settings panel submits.</summary>
/// <remarks>
/// Neither the date nor the previous level is on here: both are derived from what already stands, never supplied by
/// the client — otherwise the "since" a visitor reads would be whatever the form posted.
/// </remarks>
public class PublicSituationInput
{
    public PublicSituationLevel Level { get; set; } = PublicSituationLevel.Niedrig;

    public string Note { get; set; } = string.Empty;
}
