namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Kategorie einer Personengruppen-Akte – was die Akte darstellt. Unabhängig von der
/// Sicherheits-<see cref="Einstufung"/> (Prüffall/Verdachtsfall/…).
/// </summary>
public enum GroupsKind
{
    Grouping = 0,
    Personality = 1,
    PersonOfInterest = 2,
}

/// <summary>Anzeigetexte für die Gruppen-Kategorie (UI-frei, ohne MudBlazor-Abhängigkeit).</summary>
public static class GroupsKindDisplay
{
    public static string Name(GroupsKind kind) => kind switch
    {
        GroupsKind.Grouping => "Gruppierung",
        GroupsKind.Personality => "Persönlichkeit",
        GroupsKind.PersonOfInterest => "Person of Interest",
        _ => "—",
    };

    public static readonly IReadOnlyList<GroupsKind> All = new[]
    {
        GroupsKind.Grouping,
        GroupsKind.Personality,
        GroupsKind.PersonOfInterest,
    };
}
