namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Regeln rund um den temporären Tod: Im RP respawnt eine Person nach <see cref="RespawnMinuten"/>
/// Minuten. „Tot" gilt daher nur innerhalb dieses Fensters; danach zählt die Person effektiv wieder
/// als „Lebend". Alles wird on-read aus dem <c>TotBis</c>-Zeitstempel berechnet – kein Hintergrund-Job.
/// </summary>
public static class LifeStatusLogic
{
    /// <summary>Respawn-Zeit nach dem Tod (Minuten).</summary>
    public const int RespawnMinutes = 20;

    /// <summary>Zeitpunkt, bis zu dem ein ab <paramref name="referenz"/> eingetretener Tod gilt.</summary>
    public static DateTime DeadUntilFrom(DateTime reference) => reference.AddMinutes(RespawnMinutes);

    /// <summary>
    /// Effektiver Status: ein gespeichertes „Tot", dessen 20-Minuten-Fenster abgelaufen ist, gilt
    /// effektiv wieder als „Lebend" (Respawn).
    /// </summary>
    public static LifeStatus Effective(LifeStatus saved, DateTime? deadUntil, DateTime now)
        => saved == LifeStatus.Dead && deadUntil is { } t && t <= now
            ? LifeStatus.Alive
            : saved;

    /// <summary>True, solange das Tot-Fenster noch läuft.</summary>
    public static bool IsDeadWindow(LifeStatus saved, DateTime? deadUntil, DateTime now)
        => saved == LifeStatus.Dead && deadUntil is { } t && t > now;

    /// <summary>Verbleibende Minuten bis zum Respawn, oder null wenn kein laufendes Tot-Fenster.</summary>
    public static int? RemainingMinutes(DateTime? deadUntil, DateTime now)
        => deadUntil is { } t && t > now ? (int)Math.Ceiling((t - now).TotalMinutes) : null;
}
