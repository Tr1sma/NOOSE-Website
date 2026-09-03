using System.Linq.Expressions;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services.Public;

/// <summary>Rules of a capture report; the one place the form, the service and the tests read.</summary>
/// <remarks>
/// A capture report is a <c>Hinweis</c> with <see cref="TipKind.Ergreifung"/>, so everything <see cref="TipRules"/>
/// says still holds. What differs sits here, and only here.
/// </remarks>
public static class CaptureRules
{
    /// <summary>Anonymity is refused: money needs a recipient, and handing over a person names one anyway.</summary>
    /// <remarks>The payout already enforces this through <see cref="TipAnonymity.IsHidden"/>; here it is refused up
    /// front instead of accepted and then found unpayable.</remarks>
    public const bool AllowsAnonymity = false;

    /// <summary>Capture reports per account per rolling 24 hours.</summary>
    /// <remarks>
    /// Its own flat number, deliberately not <see cref="TipTrust.QuotaFor"/>: a busy tipping day must not block a real
    /// handover, the tier rewards good tips rather than urgency, and the role ping is the scarce resource. The
    /// one-open-report-per-notice rule below stops the obvious abuse; this is the ceiling across notices.
    /// </remarks>
    public const int PerDay = 2;

    public const int MinLocationLength = 3;

    public const int MaxLocationLength = 200;

    /// <summary>Only a wanted notice can be answered with a capture.</summary>
    /// <remarks>
    /// Positive list, not "everything except vehicles and weapons": nobody apprehends a car, but nobody apprehends a
    /// missing person or a witness appeal either. Finding someone who is missing is a sighting, so it belongs in the
    /// tip form; and the reward path books against a capture. Only <see cref="PublicWantedKind.Fahndung"/> is issued
    /// today, so this narrows nothing yet — it keeps the later kinds from silently qualifying.
    /// </remarks>
    public static bool MayReport(PublicWantedKind kind) => kind == PublicWantedKind.Fahndung;

    /// <summary>The person is still being held, so the desk has to move now.</summary>
    public static bool IsUrgent(TipKind kind, TipHandover? handover)
        => kind == TipKind.Ergreifung && handover == TipHandover.Festgehalten;

    public static bool IsCapture(TipKind kind) => kind == TipKind.Ergreifung;

    /// <summary>Query twin of <see cref="IsCapture"/>.</summary>
    public static readonly Expression<Func<Hinweis, bool>> CaptureRows =
        h => h.Kind == TipKind.Ergreifung;

    /// <summary>Still being worked on; what the desk has to see first.</summary>
    public static readonly Expression<Func<Hinweis, bool>> OpenCaptureRows =
        h => h.Kind == TipKind.Ergreifung
            && (h.Status == TipStatus.Neu || h.Status == TipStatus.InPruefung || h.Status == TipStatus.Rueckfrage);

    // refusal texts live next to the rule that produces them, so the form and the service say the same thing

    public const string NoticeRequired =
        "Eine Ergreifungsmeldung braucht die Ausschreibung, um die es geht.";

    public const string KindRefused =
        "Zu dieser Ausschreibung lässt sich keine Ergreifung melden. Nutze das Hinweisformular.";

    public const string SelfRefused =
        "Wer selbst ausgeschrieben ist, kann die eigene Ergreifung nicht melden.";

    public const string AlreadyOpen =
        "Zu dieser Ausschreibung läuft schon eine Ergreifungsmeldung von dir.";

    public const string LocationRequired =
        "Gib an, wo die Person ist — ohne den Ort kann niemand übernehmen.";
}
