using System.Linq.Expressions;
using NOOSE_Website.Data.Entities.Public;

namespace NOOSE_Website.Services.Public;

/// <summary>Decides where a tip's audit actor may be resolved and where it must stay blank.</summary>
/// <remarks>
/// The SaveChanges interceptor stamps whoever submitted, and that is the citizen account — which an agent also has.
/// On a record's timeline and chronicle that would read as "agent X reported on this person", so both read paths ask
/// here first. The change protocol under /nachweis deliberately does not: that surface is the abuse control, and it
/// is the one place where the submitting account is supposed to be visible.
/// </remarks>
public static class TipAnonymity
{
    public static bool HidesActor(string? entityType)
        => entityType is nameof(Data.Entities.Public.Hinweis) or nameof(Data.Entities.Public.HinweisNachricht);

    /// <summary>True while the promise holds; the handler projection blanks the citizen and the record file omits the tip.</summary>
    /// <remarks>
    /// One rule for two very different surfaces. On the person file the tipster history is keyed on the citizen's
    /// identity, so an anonymous tip must not appear there at all — not even as a count, which would name the tipster
    /// by arithmetic. The audited leadership resolution stays the only way to a name.
    /// </remarks>
    public static bool IsHidden(bool wantsAnonymity, DateTime? resolvedAt)
        => wantsAnonymity && resolvedAt is null;

    /// <summary>Query twin of <see cref="IsHidden"/>: the tips a record-facing surface may list at all.</summary>
    public static readonly Expression<Func<Hinweis, bool>> Disclosable =
        h => !h.WantsAnonymity || h.AnonymityResolvedAt != null;
}
