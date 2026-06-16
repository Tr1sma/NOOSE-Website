namespace NOOSE_Website.Models.Enums;

/// <summary>Group record category.</summary>
public enum GroupsKind
{
    Grouping = 0,
    Personality = 1,
    PersonOfInterest = 2,
}

/// <summary>Display labels.</summary>
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
