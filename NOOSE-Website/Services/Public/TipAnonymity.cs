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
}
