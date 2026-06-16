namespace NOOSE_Website.Models.Enums;

/// <summary>Respawn death logic.</summary>
public static class LifeStatusLogic
{
    /// <summary>Respawn delay (minutes).</summary>
    public const int RespawnMinutes = 20;

    /// <summary>Dead-until timestamp from <paramref name="reference"/>.</summary>
    public static DateTime DeadUntilFrom(DateTime reference) => reference.AddMinutes(RespawnMinutes);

    /// <summary>Effective status; expired death window counts as alive.</summary>
    public static LifeStatus Effective(LifeStatus saved, DateTime? deadUntil, DateTime now)
        => saved == LifeStatus.Dead && deadUntil is { } t && t <= now
            ? LifeStatus.Alive
            : saved;

    /// <summary>True while death window active.</summary>
    public static bool IsDeadWindow(LifeStatus saved, DateTime? deadUntil, DateTime now)
        => saved == LifeStatus.Dead && deadUntil is { } t && t > now;

    /// <summary>Minutes until respawn, or null.</summary>
    public static int? RemainingMinutes(DateTime? deadUntil, DateTime now)
        => deadUntil is { } t && t > now ? (int)Math.Ceiling((t - now).TotalMinutes) : null;
}
