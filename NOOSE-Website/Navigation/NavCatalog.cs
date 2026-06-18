using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Services;

namespace NOOSE_Website.Navigation;

/// <summary>Single source of truth for drawer entries; mirrors the former hardcoded NavMenu.</summary>
public static class NavCatalog
{
    /// <summary>All internal-agent entries in default order. Keys are stable; favorites/hidden/order reference them.</summary>
    public static readonly IReadOnlyList<NavEntry> Internal = new[]
    {
        new NavEntry("dashboard", "/", Icons.Material.Filled.SpaceDashboard, "Dashboard", NavSection.Primary, NavLinkMatch.All),
        new NavEntry("profil", "/profil", Icons.Material.Filled.AccountCircle, "Mein Profil", NavSection.Primary),
        new NavEntry("watchlist", "/watchlist", Icons.Material.Filled.Star, "Beobachtete Akten", NavSection.Primary),
        new NavEntry("personal", "/personal", Icons.Material.Filled.People, "Personal", NavSection.Primary),
        new NavEntry("brett", "/brett", Icons.Material.Filled.Campaign, "Schwarzes Brett", NavSection.Primary, BadgeKey: "acknowledgments"),

        new NavEntry("personen", "/personen", Icons.Material.Filled.Badge, "Personen-Akten", NavSection.Akten),
        new NavEntry("doks", "/doks", Icons.Material.Filled.Description, "Personen-Doks", NavSection.Akten),
        new NavEntry("dokumente", "/dokumente", Icons.Material.Filled.MenuBook, "Dokumente", NavSection.Akten),
        new NavEntry("observationen", "/observationen", Icons.Material.Filled.Visibility, "Observationen", NavSection.Akten),
        new NavEntry("fraktionen", "/fraktionen", Icons.Material.Filled.Groups, "Fraktionen", NavSection.Akten),
        new NavEntry("personengruppen", "/personengruppen", Icons.Material.Filled.Diversity3, "Personengruppen", NavSection.Akten),
        new NavEntry("parteien", "/parteien", Icons.Material.Filled.AccountBalance, "Parteien", NavSection.Akten),
        new NavEntry("operationen", "/operationen", Icons.Material.Filled.Radar, "Operationen", NavSection.Akten),
        new NavEntry("taskforces", "/taskforces", Icons.Material.Filled.Groups2, "Taskforces", NavSection.Akten),
        new NavEntry("vorgaenge", "/vorgaenge", Icons.Material.Filled.FolderSpecial, "Vorgänge", NavSection.Akten),
        new NavEntry("aufgaben", "/aufgaben", Icons.Material.Filled.AssignmentTurnedIn, "Aufgaben", NavSection.Akten),
        new NavEntry("gesetze", "/gesetze", Icons.Material.Filled.Gavel, "Gesetzbuch", NavSection.Akten),
        new NavEntry("suche", "/suche", Icons.Material.Filled.Search, "Globale Suche", NavSection.Akten),
        new NavEntry("graph", "/graph", Icons.Material.Filled.Hub, "Beziehungsgraph", NavSection.Akten),
        new NavEntry("organigramm", "/organigramm", Icons.Material.Filled.AccountTree, "Organigramm", NavSection.Akten),
        new NavEntry("kalender", "/kalender", Icons.Material.Filled.CalendarMonth, "Kalender", NavSection.Akten),
        new NavEntry("statistik", "/statistik", Icons.Material.Filled.QueryStats, "Statistik", NavSection.Akten),

        new NavEntry("admin.freigaben", "/admin/freigaben", Icons.Material.Filled.HowToReg, "Freigaben", NavSection.VerwaltungFreigaben, BadgeKey: "shares"),

        new NavEntry("admin.tags", "/admin/tags", Icons.Material.Filled.Label, "Tags", NavSection.VerwaltungFuehrung),
        new NavEntry("admin.vorlagen", "/admin/vorlagen", Icons.Material.Filled.Dvr, "Vorlagen", NavSection.VerwaltungFuehrung),
        new NavEntry("admin.custom-felder", "/admin/custom-felder", Icons.Material.Filled.Tune, "Custom-Felder", NavSection.VerwaltungFuehrung),
        new NavEntry("admin.aktualitaet", "/admin/aktualitaet", Icons.Material.Filled.Timelapse, "Aktualität", NavSection.VerwaltungFuehrung),
        new NavEntry("admin.bedrohungs-score", "/admin/bedrohungs-score", Icons.Material.Filled.Speed, "Bedrohungs-Score", NavSection.VerwaltungFuehrung),
        new NavEntry("lageberichte", "/lageberichte", Icons.Material.Filled.Assessment, "Lageberichte", NavSection.VerwaltungFuehrung),
        new NavEntry("admin.agenten", "/admin/agenten", Icons.Material.Filled.ManageAccounts, "Agenten-Verwaltung", NavSection.VerwaltungFuehrung),
        new NavEntry("admin.basisdaten", "/admin/basisdaten", Icons.Material.Filled.Storage, "Basisdaten", NavSection.VerwaltungFuehrung),

        new NavEntry("admin.module", "/admin/module", Icons.Material.Filled.School, "Ausbildungsmodule", NavSection.VerwaltungAdmin),
        new NavEntry("admin.partner-sichtbarkeit", "/admin/partner-sichtbarkeit", Icons.Material.Filled.Handshake, "Partner-Sichtbarkeit", NavSection.VerwaltungAdmin),
        new NavEntry("admin.partner-freigabe", "/admin/partner-freigabe", Icons.Material.Filled.FolderShared, "Partner-Freigabe", NavSection.VerwaltungAdmin),
        new NavEntry("admin.system", "/admin/system", Icons.Material.Filled.SettingsApplications, "System", NavSection.VerwaltungAdmin),
        new NavEntry("status", "/status", Icons.Material.Filled.MonitorHeart, "System-Status", NavSection.VerwaltungAdmin),
    };

    private static readonly Dictionary<string, NavEntry> ByKeyMap =
        Internal.ToDictionary(e => e.Key, StringComparer.Ordinal);

    /// <summary>Entries of one section, in catalog order.</summary>
    public static IEnumerable<NavEntry> Section(NavSection section)
        => Internal.Where(e => e.Section == section);

    /// <summary>Catalog entry by stable key, or null.</summary>
    public static NavEntry? ByKey(string key) => ByKeyMap.GetValueOrDefault(key);

    /// <summary>Catalog entry whose route best matches a relative path (longest prefix wins), or null.</summary>
    public static NavEntry? ByRoute(string? relativePath)
    {
        var path = (relativePath ?? string.Empty).Split('?')[0].Split('#')[0].Trim('/').ToLowerInvariant();
        if (path.Length == 0)
        {
            return ByKeyMap.GetValueOrDefault("dashboard");
        }

        NavEntry? best = null;
        var bestLen = -1;
        foreach (var e in Internal)
        {
            var route = e.Route.Trim('/').ToLowerInvariant();
            if (route.Length == 0)
            {
                continue;
            }
            if ((path == route || path.StartsWith(route + "/", StringComparison.Ordinal)) && route.Length > bestLen)
            {
                best = e;
                bestLen = route.Length;
            }
        }
        return best;
    }

    /// <summary>Partner record-type entries, filtered by the rank's allowed types (null = all).</summary>
    public static IReadOnlyList<NavEntry> PartnerRecordEntries(IReadOnlySet<string>? allowedTypes)
    {
        var list = new List<NavEntry>();
        foreach (var t in PartnerTabCatalog.All)
        {
            if (allowedTypes is null || allowedTypes.Contains(t.TypeKey))
            {
                list.Add(new NavEntry("partner." + t.TypeKey, "/" + t.RoutePrefix, PartnerIcon(t.TypeKey), t.DisplayName, NavSection.Partner));
            }
        }
        return list;
    }

    /// <summary>Icon per releasable record type.</summary>
    public static string PartnerIcon(string typeKey) => typeKey switch
    {
        nameof(Person) => Icons.Material.Filled.Badge,
        nameof(Faction) => Icons.Material.Filled.Groups,
        nameof(PersonGroup) => Icons.Material.Filled.Diversity3,
        nameof(Party) => Icons.Material.Filled.AccountBalance,
        nameof(Operation) => Icons.Material.Filled.Radar,
        nameof(Case) => Icons.Material.Filled.FolderSpecial,
        nameof(Document) => Icons.Material.Filled.MenuBook,
        nameof(Law) => Icons.Material.Filled.Gavel,
        _ => Icons.Material.Filled.Folder,
    };
}
