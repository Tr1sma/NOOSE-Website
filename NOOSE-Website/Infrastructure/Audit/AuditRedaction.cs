using NOOSE_Website.Data.Entities.Public;

namespace NOOSE_Website.Infrastructure.Audit;

/// <summary>Fields whose value never enters an audit row; the change is recorded, the content is not.</summary>
/// <remarks>
/// The change protocol on /nachweis is open to every internal agent, and it filters by nothing but the entity type
/// the reader picks. Citizen correspondence is narrower than that: a ticket thread is leadership-only, and a tip
/// carries an anonymity promise. Both used to stay out of the protocol by accident — the interceptor captures field
/// values on Modified only, and every message row was a Created row until they became editable. This registry turns
/// that accident into a rule, on the write side, so no later reader of ChangesJson can undo it.
///
/// Scoped by type, not by field name: the same "Text" on a Comment is supposed to show its before and after.
/// </remarks>
public static class AuditRedaction
{
    // CLR names, because that is what the interceptor stamps into the audit row
    private static readonly HashSet<string> Fields = new(StringComparer.Ordinal)
    {
        $"{nameof(HinweisNachricht)}.{nameof(HinweisNachricht.Text)}",
        $"{nameof(TicketNachricht)}.{nameof(TicketNachricht.Text)}",
    };

    /// <summary>Whether this field's before/after pair stays out of the audit row.</summary>
    public static bool Hides(string entityType, string property)
        => Fields.Contains($"{entityType}.{property}");
}
