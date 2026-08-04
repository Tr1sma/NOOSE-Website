using NOOSE_Website.Models.CounterIntel;

namespace NOOSE_Website.Services;

/// <summary>Pure insider-threat pattern rules over access rows (no DB). Thresholds are deliberate + tunable here.</summary>
public static class InsiderThreatRules
{
    public const int OffHoursThreshold = 15;   // accesses outside duty hours
    public const int MassAccessThreshold = 40; // distinct records opened in a single day
    public const int BurstThreshold = 30;      // accesses within a single clock hour

    /// <summary>Off-duty window: 22:00–06:00 local.</summary>
    public static bool IsOffHours(DateTime local) => local.Hour < 6 || local.Hour >= 22;

    /// <summary>Evaluate all rules; rows must already be filtered (no system rows, no read-only supervisors).</summary>
    public static List<InsiderFlag> Evaluate(IReadOnlyList<AccessRow> rows)
    {
        var flags = new List<InsiderFlag>();
        foreach (var g in rows.GroupBy(r => r.AgentId))
        {
            var name = string.IsNullOrWhiteSpace(g.First().AgentName) ? "(unbenannt)" : g.First().AgentName!;
            var href = $"/personal/{g.Key}";

            var offHours = g.Count(r => IsOffHours(r.LocalTimestamp));
            if (offHours >= OffHoursThreshold)
            {
                flags.Add(new InsiderFlag(g.Key, name, "Off-Hours",
                    $"{offHours} Zugriffe außerhalb der Dienstzeit (22–6 Uhr).", offHours, href));
            }

            var maxPerDay = g.GroupBy(r => r.LocalTimestamp.Date)
                .Select(d => d.Select(r => $"{r.EntityType}:{r.EntityId}").Distinct().Count())
                .DefaultIfEmpty(0).Max();
            if (maxPerDay >= MassAccessThreshold)
            {
                flags.Add(new InsiderFlag(g.Key, name, "Massen-Zugriff",
                    $"{maxPerDay} verschiedene Akten an einem Tag geöffnet.", maxPerDay, href));
            }

            var maxPerHour = g.GroupBy(r => new { r.LocalTimestamp.Date, r.LocalTimestamp.Hour })
                .Select(h => h.Count()).DefaultIfEmpty(0).Max();
            if (maxPerHour >= BurstThreshold)
            {
                flags.Add(new InsiderFlag(g.Key, name, "Zugriffs-Burst",
                    $"{maxPerHour} Zugriffe innerhalb einer Stunde.", maxPerHour, href));
            }
        }
        return flags.OrderByDescending(f => f.Severity).ToList();
    }
}
