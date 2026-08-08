using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.CounterIntel;

/// <summary>
/// The three patterns the cockpit shipped with before rules became editable. Seeded by the migration
/// with these exact ids, and re-creatable from the panel once someone has edited them into the ground.
/// </summary>
public static class CounterIntelRuleDefaults
{
    public const string OffHoursId = "11111111-c0de-4a01-9000-000000000001";
    public const string MassAccessId = "11111111-c0de-4a01-9000-000000000002";
    public const string BurstId = "11111111-c0de-4a01-9000-000000000003";

    /// <summary>Id, name, description, severity, order and definition of every built-in rule.</summary>
    public static IReadOnlyList<CounterIntelRuleView> All =>
    [
        new(OffHoursId, "Off-Hours",
            "Zugriffe außerhalb der Dienstzeit (22–6 Uhr) über den gesamten Zeitraum.",
            CounterIntelSeverity.Warning, IsActive: true, Order: 10,
            new CounterIntelRuleDefinition
            {
                WindowDays = 30,
                Actions = [CounterIntelActionKind.Read],
                FromHour = 22,
                ToHour = 6,
                CountMode = CounterIntelCountMode.Events,
                Bucket = CounterIntelBucket.Window,
                Threshold = 15,
            }),

        new(MassAccessId, "Massen-Zugriff",
            "Auffällig viele verschiedene Akten an einem einzigen Tag geöffnet.",
            CounterIntelSeverity.High, IsActive: true, Order: 20,
            new CounterIntelRuleDefinition
            {
                WindowDays = 30,
                Actions = [CounterIntelActionKind.Read],
                CountMode = CounterIntelCountMode.DistinctRecords,
                Bucket = CounterIntelBucket.Day,
                Threshold = 40,
            }),

        new(BurstId, "Zugriffs-Burst",
            "Sehr viele Zugriffe innerhalb einer einzigen Stunde.",
            CounterIntelSeverity.Warning, IsActive: true, Order: 30,
            new CounterIntelRuleDefinition
            {
                WindowDays = 30,
                Actions = [CounterIntelActionKind.Read],
                CountMode = CounterIntelCountMode.Events,
                Bucket = CounterIntelBucket.Hour,
                Threshold = 30,
            }),
    ];
}
