namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Regeln rund um den temporären Tod: Im RP respawnt eine Person nach <see cref="RespawnMinuten"/>
/// Minuten. „Tot" gilt daher nur innerhalb dieses Fensters; danach zählt die Person effektiv wieder
/// als „Lebend". Alles wird on-read aus dem <c>TotBis</c>-Zeitstempel berechnet – kein Hintergrund-Job.
/// </summary>
public static class LebensstatusLogic
{
    /// <summary>Respawn-Zeit nach dem Tod (Minuten).</summary>
    public const int RespawnMinuten = 20;

    /// <summary>Zeitpunkt, bis zu dem ein ab <paramref name="referenz"/> eingetretener Tod gilt.</summary>
    public static DateTime TotBisAb(DateTime referenz) => referenz.AddMinutes(RespawnMinuten);

    /// <summary>
    /// Effektiver Status: ein gespeichertes „Tot", dessen 20-Minuten-Fenster abgelaufen ist, gilt
    /// effektiv wieder als „Lebend" (Respawn).
    /// </summary>
    public static Lebensstatus Effektiv(Lebensstatus gespeichert, DateTime? totBis, DateTime jetzt)
        => gespeichert == Lebensstatus.Tot && totBis is { } t && t <= jetzt
            ? Lebensstatus.Lebend
            : gespeichert;

    /// <summary>True, solange das Tot-Fenster noch läuft.</summary>
    public static bool IstTotFenster(Lebensstatus gespeichert, DateTime? totBis, DateTime jetzt)
        => gespeichert == Lebensstatus.Tot && totBis is { } t && t > jetzt;

    /// <summary>Verbleibende Minuten bis zum Respawn, oder null wenn kein laufendes Tot-Fenster.</summary>
    public static int? VerbleibendeMinuten(DateTime? totBis, DateTime jetzt)
        => totBis is { } t && t > jetzt ? (int)Math.Ceiling((t - jetzt).TotalMinutes) : null;
}
