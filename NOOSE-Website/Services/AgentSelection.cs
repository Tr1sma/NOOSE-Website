using System.Linq.Expressions;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Central rule for which agents an option list may contain.</summary>
/// <remarks>
/// Four predicates on purpose: pickers need current staff, log filters need everyone who ever acted, the
/// audit viewer additionally needs team leads and partners, and the personnel area needs everyone who has
/// a file. Team leads are read-only supervision and RP-wide invisible, so outside the audit viewer they
/// appear in no list at all — not even with the admin flag on top. Partners are external and stay selectable
/// only where a share is being granted: PartnerShareDialog, the Admin partner surfaces and the PersonnelList
/// partner tab. MentionService is a deliberate exception and keeps its own predicate. Callers own ordering.
/// </remarks>
public static class AgentSelection
{
    // one literal of each rule; declaration order matters, the compiled twin reads the field above it
    private static readonly Expression<Func<Agent, bool>> SelectableRule =
        a => a.Status == AgentStatus.Active && !a.IsTeamLead && a.PartnerAgency == null;

    private static readonly Func<Agent, bool> SelectableInMemory = SelectableRule.Compile();

    private static readonly Expression<Func<Agent, bool>> ListableRule =
        a => !string.IsNullOrEmpty(a.Codename) && !a.IsTeamLead && a.PartnerAgency == null;

    private static readonly Expression<Func<Agent, bool>> AuditFilterableRule =
        a => !string.IsNullOrEmpty(a.Codename);

    private static readonly Expression<Func<Agent, bool>> PersonnelFileRule =
        a => a.Status != AgentStatus.Applicant && a.Status != AgentStatus.Blocked && !a.IsTeamLead;

    private static readonly Func<Agent, bool> PersonnelFileInMemory = PersonnelFileRule.Compile();

    /// <summary>Agents a picker, dropdown or roster may offer: active in-house staff only.</summary>
    public static IQueryable<Agent> OnlySelectable(this IQueryable<Agent> agents) => agents.Where(SelectableRule);

    /// <summary>Agents a log or audit filter may offer.</summary>
    /// <remarks>
    /// Terminated and blocked accounts stay listed: their past rows are exactly what those filters exist
    /// for. A blank codename means never released, so there is no agent to name.
    /// </remarks>
    public static IQueryable<Agent> OnlyListable(this IQueryable<Agent> agents) => agents.Where(ListableRule);

    /// <summary>Agents the audit viewer's agent filter may offer: everyone who ever acted.</summary>
    /// <remarks>
    /// Deliberately wider than <see cref="OnlyListable"/>: team leads and partners stay listed so the
    /// counter-intelligence tabs can filter their log rows. Audit-viewer use only — nowhere else.
    /// </remarks>
    public static IQueryable<Agent> OnlyAuditFilterable(this IQueryable<Agent> agents) => agents.Where(AuditFilterableRule);

    /// <summary>Agents that have a personnel file: the roster of <c>/personal</c> and the search over it.</summary>
    /// <remarks>
    /// Wider than <see cref="OnlySelectable"/> — a terminated agent keeps their file, and a partner account has one
    /// too. Narrower in the other direction: an applicant is not an agent (they belong to recruiting), a blocked
    /// account is hidden, and a team lead is invisible RP-wide. The global search MUST go through this, or it hands
    /// out exactly the accounts the page hides.
    /// </remarks>
    public static IQueryable<Agent> OnlyWithPersonnelFile(this IQueryable<Agent> agents) => agents.Where(PersonnelFileRule);

    /// <summary>Selectable check for rows already materialized; inside a query use OnlySelectable.</summary>
    public static bool IsSelectable(Agent agent) => SelectableInMemory(agent);

    /// <summary>Personnel-file check for rows already materialized; inside a query use OnlyWithPersonnelFile.</summary>
    public static bool HasPersonnelFile(Agent agent) => PersonnelFileInMemory(agent);
}
