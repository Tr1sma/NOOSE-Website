using System.Linq.Expressions;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services.Public;

/// <summary>Limits and state transitions of a citizen tip; the one place both the service and the form read.</summary>
/// <remarks>
/// The quota lives here rather than in the rate-limiting middleware because the submission travels over SignalR, which
/// never reaches the middleware. The HTTP policy guards the file endpoint; this number is the actual limit.
/// </remarks>
public static class TipRules
{
    /// <summary>Below this a submission is not a tip; it is a click.</summary>
    public const int MinLength = 30;

    public const int MaxLength = 5000;

    /// <summary>Length of one message in either thread.</summary>
    public const int MaxMessageLength = 2000;

    /// <summary>Submissions per account per rolling 24 hours.</summary>
    public const int PerDay = 5;

    public static readonly TimeSpan QuotaWindow = TimeSpan.FromHours(24);

    /// <summary>Still being worked on; drives the inbox badge and the citizen's "offen" list.</summary>
    public static bool IsOpen(TipStatus status)
        => status is TipStatus.Neu or TipStatus.InPruefung or TipStatus.Rueckfrage;

    /// <summary>Query twin of <see cref="IsOpen"/>; the priority stamper only ever touches these rows.</summary>
    public static readonly Expression<Func<Hinweis, bool>> OpenRows =
        h => h.Status == TipStatus.Neu || h.Status == TipStatus.InPruefung || h.Status == TipStatus.Rueckfrage;

    /// <summary>Counts towards the tipster's trust tier.</summary>
    public static bool CountsAsConfirmed(TipStatus status)
        => status is TipStatus.Bestaetigt or TipStatus.FuehrteZurErgreifung;

    /// <summary>Query twin of <see cref="CountsAsConfirmed"/>; the trust counter is recomputed from these rows.</summary>
    public static readonly Expression<Func<Hinweis, bool>> ConfirmedRows =
        h => h.Status == TipStatus.Bestaetigt || h.Status == TipStatus.FuehrteZurErgreifung;

    /// <summary>Decided; the conversation is closed for both sides.</summary>
    public static bool IsClosed(TipStatus status)
        => status is TipStatus.Bestaetigt or TipStatus.Verworfen or TipStatus.FuehrteZurErgreifung;

    /// <summary>Allowed status moves; anything else is refused rather than silently applied.</summary>
    /// <remarks>
    /// A decided tip may be reopened into <see cref="TipStatus.InPruefung"/>: new information arrives after a rejection
    /// often enough, and the alternative is a citizen filing the same tip again. Only the reward status is a one-way
    /// door — it is what the payout phase books against.
    /// </remarks>
    public static bool IsTransitionAllowed(TipStatus from, TipStatus to)
    {
        if (from == to)
        {
            return false;
        }
        return from switch
        {
            TipStatus.Neu => to is TipStatus.InPruefung or TipStatus.Rueckfrage or TipStatus.Verworfen,
            TipStatus.InPruefung => to is TipStatus.Rueckfrage or TipStatus.Bestaetigt or TipStatus.Verworfen
                or TipStatus.FuehrteZurErgreifung,
            TipStatus.Rueckfrage => to is TipStatus.InPruefung or TipStatus.Bestaetigt or TipStatus.Verworfen
                or TipStatus.FuehrteZurErgreifung,
            TipStatus.Bestaetigt => to is TipStatus.InPruefung or TipStatus.Verworfen or TipStatus.FuehrteZurErgreifung,
            TipStatus.Verworfen => to is TipStatus.InPruefung,
            TipStatus.FuehrteZurErgreifung => false,
            _ => false,
        };
    }

    /// <summary>Statuses an agent may set by hand, given the current one.</summary>
    public static IReadOnlyList<TipStatus> AllowedTargets(TipStatus from)
        => TipStatusDisplay.All.Where(t => IsTransitionAllowed(from, t)).ToList();
}
