namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Zielgruppe einer Ankündigung/eines Behörden-Broadcasts – Phase 6. Steuert sowohl, wer den Eintrag am
/// Schwarzen Brett sieht, als auch (bei Broadcast) wer eine Glocken-Meldung erhält. Eine gezielte Zielgruppe
/// (alles außer <see cref="AlleAktiven"/>) ist der Führung vorbehalten.
/// </summary>
public enum AnnouncementAudience
{
    /// <summary>Alle aktiven Agenten (Standard, auch für offene Brett-Notizen).</summary>
    AllActive = 0,
    /// <summary>Nur die einer bestimmten Taskforce zugeteilten Agenten (Ziel-Id = Taskforce-Id).</summary>
    Taskforce = 1,
    /// <summary>Nur Agenten der TRU-Einheit (<see cref="Data.Entities.Agent.IstTRU"/>).</summary>
    TruUnit = 2,
    /// <summary>Nur Agenten ab einem Mindest-Dienstgrad (Ziel = <c>MinDienstgrad</c>).</summary>
    FromRank = 3,
    /// <summary>Nur Agenten der HRB-Einheit (<see cref="Data.Entities.Agent.IstHRB"/>).</summary>
    HrbUnit = 4,
}

/// <summary>Anzeigetexte für die Ankündigungs-Zielgruppe (UI-frei).</summary>
public static class AnnouncementAudienceDisplay
{
    public static string Name(AnnouncementAudience audience) => audience switch
    {
        AnnouncementAudience.AllActive => "Alle aktiven Agenten",
        AnnouncementAudience.Taskforce => "Bestimmte Taskforce",
        AnnouncementAudience.TruUnit => "TRU-Einheit",
        AnnouncementAudience.HrbUnit => "HRB-Einheit",
        AnnouncementAudience.FromRank => "Ab Dienstgrad",
        _ => "—",
    };

    public static readonly IReadOnlyList<AnnouncementAudience> All = new[]
    {
        AnnouncementAudience.AllActive,
        AnnouncementAudience.Taskforce,
        AnnouncementAudience.TruUnit,
        AnnouncementAudience.HrbUnit,
        AnnouncementAudience.FromRank,
    };
}
