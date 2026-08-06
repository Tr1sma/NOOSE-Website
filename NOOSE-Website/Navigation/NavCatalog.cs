using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Services;

namespace NOOSE_Website.Navigation;

/// <summary>Single source of truth for drawer entries; mirrors the former hardcoded NavMenu.</summary>
public static class NavCatalog
{
    /// <summary>All internal-agent entries in default order. Keys are stable; favorites/hidden/order reference them.</summary>
    public static readonly IReadOnlyList<NavEntry> Internal = new[]
    {
        new NavEntry("dashboard", "/dashboard", Icons.Material.Filled.SpaceDashboard, "Dashboard", NavSection.Primary, NavArea.Primary, NavLinkMatch.All,
            Description: "Lagezentrum mit Kennzahlen und letzten Änderungen"),
        new NavEntry("suche", "/suche", Icons.Material.Filled.Search, "Globale Suche", NavSection.Primary, NavArea.Primary,
            Description: "Durchsucht alle Akten, Dokumente und Inhalte"),

        new NavEntry("profil", "/profil", Icons.Material.Filled.AccountCircle, "Mein Profil", NavSection.MeinDienst, NavArea.MeinDienst,
            Description: "Eigene Stammdaten, Codename und Kontaktangaben"),
        new NavEntry("watchlist", "/watchlist", Icons.Material.Filled.Star, "Beobachtete Akten", NavSection.MeinDienst, NavArea.MeinDienst,
            Description: "Akten, denen du folgst — mit Änderungshinweis"),
        new NavEntry("aufgaben", "/aufgaben", Icons.Material.Filled.AssignmentTurnedIn, "Aufgaben-Board", NavSection.MeinDienst, NavArea.MeinDienst,
            Description: "To-dos des Teams als Board — für alle Agenten sichtbar"),
        new NavEntry("aktivitaeten", "/aktivitaeten", Icons.Material.Filled.Bolt, "Dienst-Aktivitäten", NavSection.MeinDienst, NavArea.MeinDienst,
            Description: "Diensteinträge, die Agenten über sich selbst führen — für alle sichtbar"),
        new NavEntry("abmeldungen", "/abmeldungen", Icons.Material.Filled.EventBusy, "Abmeldungen", NavSection.MeinDienst, NavArea.MeinDienst,
            Description: "Eigene Abwesenheiten melden und einsehen"),

        new NavEntry("personen", "/personen", Icons.Material.Filled.Badge, "Personen-Akten", NavSection.Akten, NavArea.Akten,
            Description: "Zentrale Akte je Person — alles zu einer Person an einem Ort"),
        new NavEntry("fraktionen", "/fraktionen", Icons.Material.Filled.Groups, "Fraktionen", NavSection.Akten, NavArea.Akten,
            Description: "Akten organisierter Gruppierungen mit Mitgliedern und Rängen"),
        new NavEntry("personengruppen", "/personengruppen", Icons.Material.Filled.Diversity3, "Personengruppen", NavSection.Akten, NavArea.Akten,
            Description: "Lose Zusammenschlüsse ohne feste Fraktionsstruktur"),
        new NavEntry("parteien", "/parteien", Icons.Material.Filled.AccountBalance, "Parteien", NavSection.Akten, NavArea.Akten,
            Description: "Politische Parteien mit Mitgliedern und Ausrichtung"),
        new NavEntry("asservatenkammer", "/asservatenkammer", Icons.Material.Filled.Inventory2, "Asservatenkammer", NavSection.Akten, NavArea.Akten,
            Description: "Ein- und Auslagerung von Gegenständen mit Bestand, Bild und Besitzer"),

        new NavEntry("vorgaenge", "/vorgaenge", Icons.Material.Filled.FolderSpecial, "Vorgänge", NavSection.VorgaengeEinsaetze, NavArea.Akten,
            Description: "Übergeordnete Ermittlungsakte, die mehrere Akten bündelt"),
        new NavEntry("operationen", "/operationen", Icons.Material.Filled.Radar, "Operationen", NavSection.VorgaengeEinsaetze, NavArea.Akten,
            Description: "Einsatzbericht zu einem konkreten Einsatz mit Ort und Zeit"),
        new NavEntry("taskforces", "/taskforces", Icons.Material.Filled.Groups2, "Taskforces", NavSection.VorgaengeEinsaetze, NavArea.Akten,
            Description: "Einheit aus Agenten mit Auftrag und Genehmigung"),

        new NavEntry("fahndung", "/fahndung", Icons.Material.Filled.PersonSearch, "Fahndung & Überwachung", NavSection.Fahndung, NavArea.Ermittlung,
            Description: "Gesuchte Personen, Observationen und Vernehmungen"),
        new NavEntry("informanten", "/informanten", Icons.Material.Filled.VisibilityOff, "Informanten", NavSection.Fahndung, NavArea.Ermittlung,
            Description: "Vertrauliche Quellen (V-Personen) — Zeilen-Sichtbarkeit je Führungsagent"),
        new NavEntry("entfuehrungen", "/entfuehrungen", Icons.Material.Filled.PersonOff, "Entführungen", NavSection.Fahndung, NavArea.Ermittlung,
            Description: "Entführungen von NOOSE-Agenten: Täter, Informationsabfluss und kompromittierte Akten"),

        new NavEntry("dokumente", "/dokumente", Icons.Material.Filled.MenuBook, "Dokumenten-Bibliothek", NavSection.Wissen, NavArea.Ermittlung,
            Description: "Zentrale Ablage aller behördlichen Dokumente"),
        new NavEntry("gesetze", "/gesetze", Icons.Material.Filled.Gavel, "Gesetzbuch", NavSection.Wissen, NavArea.Ermittlung,
            Description: "Paragrafen und Rechtsgrundlagen zum Nachschlagen"),

        new NavEntry("graph", "/graph", Icons.Material.Filled.Hub, "Beziehungsgraph", NavSection.Analyse, NavArea.Ermittlung,
            Description: "Verknüpfungen zwischen Akten als Netzdiagramm"),
        // key stays "chronik": stored favorites reference it, and LegacyRoutes aliases onto it
        new NavEntry("chronik", "/nachweis", Icons.Material.Filled.FactCheck, "Nachweis & Gegenaufklärung", NavSection.Analyse, NavArea.Ermittlung,
            Description: "Chronik, Änderungs- und Zugriffsprotokoll, Gegenaufklärung"),
        new NavEntry("hinweise", "/ermittlungshinweise", Icons.Material.Filled.Lightbulb, "Ermittlungshinweise", NavSection.Analyse, NavArea.Ermittlung,
            Description: "Algorithmische Hinweise: mögliche Verbindungen, Konflikte, veraltete Einstufungen"),
        new NavEntry("ki", "/ki-assistent", Icons.Material.Filled.SmartToy, "KI-Assistent", NavSection.Analyse, NavArea.Ermittlung,
            Description: "KI-gestütztes Formulieren, Zusammenfassen und Analysieren von Texten"),
        new NavEntry("statistik", "/statistik", Icons.Material.Filled.QueryStats, "Statistik", NavSection.Analyse, NavArea.Ermittlung,
            Description: "Auswertungen, Kennzahlen und Lageberichte"),
        new NavEntry("organigramm", "/organigramm", Icons.Material.Filled.AccountTree, "Organigramm", NavSection.Analyse, NavArea.Dienststelle,
            Description: "Aufbau der Behörde nach Dienstgrad und Einheit"),

        new NavEntry("brett", "/brett", Icons.Material.Filled.Campaign, "Schwarzes Brett", NavSection.Dienststelle, NavArea.Dienststelle, BadgeKey: "acknowledgments",
            Description: "Behördliche Ankündigungen, teils quittierungspflichtig"),
        new NavEntry("personal", "/personal", Icons.Material.Filled.People, "Personal", NavSection.Dienststelle, NavArea.Dienststelle,
            Description: "Agenten der Behörde mit Personalakte und Dienstgrad"),
        new NavEntry("bestenliste", "/bestenliste", Icons.Material.Filled.EmojiEvents, "Bestenliste", NavSection.Dienststelle, NavArea.Dienststelle,
            Description: "Team-Ranking nach dokumentierter Ermittlungsarbeit"),
        new NavEntry("besprechungen", "/besprechungen", Icons.Material.Filled.Groups, "Besprechungen", NavSection.Dienststelle, NavArea.Dienststelle,
            Description: "Dienstbesprechungen mit Tagesordnung und Protokoll"),
        new NavEntry("kalender", "/kalender", Icons.Material.Filled.CalendarMonth, "Kalender", NavSection.Dienststelle, NavArea.Dienststelle,
            Description: "Termine der Behörde in der Monatsansicht"),

        new NavEntry("admin.freigaben", "/admin/freigaben", Icons.Material.Filled.HowToReg, "Freigaben", NavSection.VerwaltungFreigaben, NavArea.Verwaltung, BadgeKey: "shares",
            Description: "Posteingang für Anträge, Registrierungen und Freigaben"),

        new NavEntry("bewerbungen", "/bewerbungen", Icons.Material.Filled.HowToReg, "Bewerbungen", NavSection.VerwaltungBewerbungen, NavArea.Verwaltung,
            Description: "Eingang, Sperren, Anschreiben-Vorlagen und Eignungstests"),

        new NavEntry("admin.agenten", "/admin/agenten", Icons.Material.Filled.ManageAccounts, "Agenten-Verwaltung", NavSection.VerwaltungFuehrung, NavArea.Verwaltung,
            Description: "Dienstgrad, Status und Rechte der Agenten setzen"),
        new NavEntry("kasse", "/kasse", Icons.Material.Filled.AccountBalanceWallet, "Kasse", NavSection.VerwaltungFuehrung, NavArea.Verwaltung,
            Description: "NOOSE-Kasse: Schwarz- und Grüngeld buchen, Vorlagen und Statistik"),
        new NavEntry("einstellungen", "/einstellungen", Icons.Material.Filled.SettingsApplications, "Einstellungen", NavSection.VerwaltungFuehrung, NavArea.Verwaltung,
            Description: "System, Vorlagen, Tags, Score, Partner und Protokoll an einer Stelle"),
        new NavEntry("papierkorb", "/papierkorb", Icons.Material.Filled.Delete, "Papierkorb", NavSection.VerwaltungFuehrung, NavArea.Verwaltung,
            Description: "Gelöschte Akten aller Typen wiederherstellen"),
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
                list.Add(new NavEntry("partner." + t.TypeKey, "/" + t.RoutePrefix, PartnerIcon(t.TypeKey), t.DisplayName, NavSection.Partner, NavArea.Partner));
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
        nameof(Taskforce) => Icons.Material.Filled.Groups2,
        nameof(Case) => Icons.Material.Filled.FolderSpecial,
        nameof(Document) => Icons.Material.Filled.MenuBook,
        nameof(Law) => Icons.Material.Filled.Gavel,
        _ => Icons.Material.Filled.Folder,
    };
}
