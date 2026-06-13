namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Zielgruppe einer Ankündigung/eines Behörden-Broadcasts – Phase 6. Steuert sowohl, wer den Eintrag am
/// Schwarzen Brett sieht, als auch (bei Broadcast) wer eine Glocken-Meldung erhält. Eine gezielte Zielgruppe
/// (alles außer <see cref="AlleAktiven"/>) ist der Führung vorbehalten.
/// </summary>
public enum AnkuendigungZielgruppe
{
    /// <summary>Alle aktiven Agenten (Standard, auch für offene Brett-Notizen).</summary>
    AlleAktiven = 0,
    /// <summary>Nur die einer bestimmten Taskforce zugeteilten Agenten (Ziel-Id = Taskforce-Id).</summary>
    Taskforce = 1,
    /// <summary>Nur Agenten der TRU-Einheit (<see cref="Data.Entities.Agent.IstTRU"/>).</summary>
    TruEinheit = 2,
    /// <summary>Nur Agenten ab einem Mindest-Dienstgrad (Ziel = <c>MinDienstgrad</c>).</summary>
    AbDienstgrad = 3,
    /// <summary>Nur Agenten der HRB-Einheit (<see cref="Data.Entities.Agent.IstHRB"/>).</summary>
    HrbEinheit = 4,
}

/// <summary>Anzeigetexte für die Ankündigungs-Zielgruppe (UI-frei).</summary>
public static class AnkuendigungZielgruppeAnzeige
{
    public static string Name(AnkuendigungZielgruppe zielgruppe) => zielgruppe switch
    {
        AnkuendigungZielgruppe.AlleAktiven => "Alle aktiven Agenten",
        AnkuendigungZielgruppe.Taskforce => "Bestimmte Taskforce",
        AnkuendigungZielgruppe.TruEinheit => "TRU-Einheit",
        AnkuendigungZielgruppe.HrbEinheit => "HRB-Einheit",
        AnkuendigungZielgruppe.AbDienstgrad => "Ab Dienstgrad",
        _ => "—",
    };

    public static readonly IReadOnlyList<AnkuendigungZielgruppe> Alle = new[]
    {
        AnkuendigungZielgruppe.AlleAktiven,
        AnkuendigungZielgruppe.Taskforce,
        AnkuendigungZielgruppe.TruEinheit,
        AnkuendigungZielgruppe.HrbEinheit,
        AnkuendigungZielgruppe.AbDienstgrad,
    };
}
