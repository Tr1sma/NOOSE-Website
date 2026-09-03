using NOOSE_Website.Data.Entities.Public;

namespace NOOSE_Website.Services.Public;

/// <summary>When a citizen's name is settled, and the promise the form makes before it is; service and page read both.</summary>
/// <remarks>
/// Derived from the name itself rather than from a lock column: "already named" and "locked" are the same fact, and a
/// second flag could disagree with it. Consequence, intended: an account that already carries a name is locked from
/// the moment this ships.
/// <para>
/// The lock is what makes the name usable as an identity claim elsewhere — the objection gate compares it against a
/// published notice, and a freely rewritable name would have made that gate self-asserted.
/// </para>
/// </remarks>
public static class BuergerNameRules
{
    /// <summary>True once both parts are on file; from then on only leadership may change them.</summary>
    public static bool IsLocked(string? firstName, string? lastName)
        => !string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(lastName);

    /// <summary>Overload for the entity; null (no profile yet) is never locked.</summary>
    public static bool IsLocked(BuergerProfil? profile)
        => profile is not null && IsLocked(profile.FirstName, profile.LastName);

    /// <summary>True when the two names mean the same entry; a repeated save of an unchanged name is not a change.</summary>
    public static bool Same(string? firstA, string? lastA, string? firstB, string? lastB)
        => string.Equals((firstA ?? string.Empty).Trim(), (firstB ?? string.Empty).Trim(), StringComparison.Ordinal)
            && string.Equals((lastA ?? string.Empty).Trim(), (lastB ?? string.Empty).Trim(), StringComparison.Ordinal);

    /// <summary>What the form says before the first save, and what the refusal says afterwards.</summary>
    public const string LockWarning =
        "Dieser Name lässt sich danach nicht mehr selbst ändern. Er muss deinem IC-Namen entsprechen — "
        + "Fantasie- oder Troll-Namen führen zum dauerhaften Ausschluss von dieser Website. "
        + "Eine spätere Korrektur nimmt nur die Führungsebene vor.";

    /// <summary>The checkbox the citizen has to tick before the name is written the first time.</summary>
    public const string LockConsent =
        "Ich habe verstanden, dass dieser Name endgültig ist und meinem IC-Namen entsprechen muss.";

    /// <summary>Refusal of a self-service rename.</summary>
    public const string LockedMessage =
        "Dein Name steht bereits fest und lässt sich nicht mehr selbst ändern. "
        + "Wende dich für eine Korrektur an die Führungsebene.";
}
