using System.Linq.Expressions;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services.Public;

/// <summary>Limits and state transitions of a citizen objection; the one place service and form both read.</summary>
/// <remarks>
/// The quota lives here rather than in the rate-limiting middleware because the submission travels over SignalR,
/// which never reaches the middleware.
/// </remarks>
public static class ObjectionRules
{
    /// <summary>Below this a submission is not an objection; it is a click.</summary>
    public const int MinLength = 30;

    public const int MaxLength = 4000;

    /// <summary>Length of the one answer the agency writes.</summary>
    public const int MaxNoteLength = 2000;

    /// <summary>Objections per account per rolling 24 hours.</summary>
    public const int PerDay = 3;

    public static readonly TimeSpan QuotaWindow = TimeSpan.FromHours(24);

    /// <summary>Still awaiting a decision; drives the desk tab and the "one per notice" cap.</summary>
    public static bool IsOpen(ObjectionStatus status)
        => status is ObjectionStatus.Neu or ObjectionStatus.InPruefung;

    /// <summary>Query twin of <see cref="IsOpen"/>.</summary>
    public static readonly Expression<Func<FahndungEinspruch, bool>> OpenRows =
        e => e.Status == ObjectionStatus.Neu || e.Status == ObjectionStatus.InPruefung;

    /// <summary>Allowed status moves; anything else is refused rather than silently applied.</summary>
    /// <remarks>
    /// A decided objection may go back into review: new evidence arrives after a rejection often enough, and the
    /// alternative is the citizen filing the same objection again and spending a quota slot on it.
    /// </remarks>
    public static bool IsTransitionAllowed(ObjectionStatus from, ObjectionStatus to)
    {
        if (from == to)
        {
            return false;
        }
        return from switch
        {
            ObjectionStatus.Neu => to is ObjectionStatus.InPruefung or ObjectionStatus.Angenommen
                or ObjectionStatus.Abgelehnt,
            ObjectionStatus.InPruefung => to is ObjectionStatus.Angenommen or ObjectionStatus.Abgelehnt,
            ObjectionStatus.Angenommen => to is ObjectionStatus.InPruefung,
            ObjectionStatus.Abgelehnt => to is ObjectionStatus.InPruefung,
            _ => false,
        };
    }

    /// <summary>Statuses a handler may set by hand, given the current one.</summary>
    public static IReadOnlyList<ObjectionStatus> AllowedTargets(ObjectionStatus from)
        => ObjectionStatusDisplay.All.Where(t => IsTransitionAllowed(from, t)).ToList();
}
