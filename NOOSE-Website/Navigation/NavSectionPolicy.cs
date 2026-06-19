using NOOSE_Website.Authorization;

namespace NOOSE_Website.Navigation;

/// <summary>Maps a nav section to the same policy its drawer wrapper uses, plus a display name.</summary>
public static class NavSectionPolicy
{
    /// <summary>Gating policy for a section (favorites/customizer reuse the exact drawer policies).</summary>
    public static string For(NavSection section) => section switch
    {
        NavSection.VerwaltungFreigaben => Policies.HighestClassificationPage,
        NavSection.VerwaltungBewerbungen => Policies.HrbOrLeadership,
        NavSection.VerwaltungFuehrung => Policies.LeadershipPage,
        NavSection.VerwaltungAdmin => Policies.AdminPage,
        NavSection.Partner => Policies.PartnerView,
        _ => Policies.InternalAgent,
    };

    /// <summary>German heading for a section in the customizer.</summary>
    public static string Name(NavSection section) => section switch
    {
        NavSection.Primary => "Allgemein",
        NavSection.Akten => "Akten",
        NavSection.VerwaltungFreigaben => "Verwaltung",
        NavSection.VerwaltungBewerbungen => "Verwaltung · Bewerbungen",
        NavSection.VerwaltungFuehrung => "Verwaltung · Führung",
        NavSection.VerwaltungAdmin => "Verwaltung · Admin",
        NavSection.Partner => "Freigegebene Akten",
        _ => string.Empty,
    };

    /// <summary>Sections shown to an internal agent, in drawer order.</summary>
    public static readonly NavSection[] InternalSections =
    [
        NavSection.Primary, NavSection.Akten,
        NavSection.VerwaltungFreigaben, NavSection.VerwaltungBewerbungen,
        NavSection.VerwaltungFuehrung, NavSection.VerwaltungAdmin,
    ];
}
