using MudBlazor;
using NOOSE_Website.Models.Gamification;

namespace NOOSE_Website.Services;

/// <summary>A milestone badge and the condition that earns it.</summary>
public sealed record BadgeDefinition(string Key, string Label, string Icon, string Description, Func<AgentStats, bool> Earned);

/// <summary>Static catalog of milestone badges. Extend here — no migration needed.</summary>
public static class BadgeCatalog
{
    public static readonly IReadOnlyList<BadgeDefinition> All = new List<BadgeDefinition>
    {
        new("erste-akte", "Erste Akte", Icons.Material.Filled.FolderOpen,
            "Erste Akte angelegt.", s => s.Records >= 1),
        new("aktenfuchs", "Aktenfuchs", Icons.Material.Filled.Folder,
            "25 Akten angelegt.", s => s.Records >= 25),
        new("akten-veteran", "Akten-Veteran", Icons.Material.Filled.FolderSpecial,
            "100 Akten angelegt.", s => s.Records >= 100),
        new("dokumentar", "Dokumentar", Icons.Material.Filled.Description,
            "25 Doks verfasst.", s => s.Docs >= 25),
        new("netzwerker", "Netzwerker", Icons.Material.Filled.Hub,
            "25 Verknüpfungen erstellt.", s => s.Links >= 25),
        new("analyst", "Analyst", Icons.Material.Filled.Shield,
            "10 Einstufungen vorgenommen.", s => s.Classifications >= 10),
        new("beobachter", "Beobachter", Icons.Material.Filled.Visibility,
            "20 Observationen dokumentiert.", s => s.Observations >= 20),
        new("fallabschliesser", "Fallabschließer", Icons.Material.Filled.TaskAlt,
            "10 Vorgänge abgeschlossen.", s => s.SolvedCases >= 10),
        new("allrounder", "Allrounder", Icons.Material.Filled.Diversity3,
            "Vielseitig aktiv: Akten, Doks und abgeschlossene Vorgänge.",
            s => s.Records >= 10 && s.Docs >= 10 && s.SolvedCases >= 5),
    };

    private static readonly Dictionary<string, BadgeDefinition> ByKey =
        All.ToDictionary(b => b.Key, StringComparer.Ordinal);

    public static BadgeDefinition? Find(string key) => ByKey.GetValueOrDefault(key);
}
