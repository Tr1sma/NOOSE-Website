using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.OrgChart;

/// <summary>
/// Aufbereitete Daten für die Organigramm-/Personalübersichts-Seite: die Dienstgrad-Ebenen des aktiven
/// NOOSE-Personals, der TRU- und HRB-Querschnitt und die für den Betrachter sichtbaren, genehmigten Taskforces
/// mit ihrer Besetzung. Rein lesend.
/// </summary>
public sealed record OrgChartData(
    IReadOnlyList<RankGroup> Ranks,
    IReadOnlyList<Agent> Tru,
    IReadOnlyList<Agent> Hrb,
    IReadOnlyList<TaskforceStaffing> Taskforces);

/// <summary>Alle aktiven Agenten eines Dienstgrades (für eine Hierarchie-Ebene).</summary>
public sealed record RankGroup(Rank Rank, IReadOnlyList<Agent> Agents);

/// <summary>Eine Taskforce samt ihrer (sichtbaren) Besetzung – Leitung zuerst.</summary>
public sealed record TaskforceStaffing(Taskforce Taskforce, IReadOnlyList<TaskforceAgent> Members);
