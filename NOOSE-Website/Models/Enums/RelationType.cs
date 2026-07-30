namespace NOOSE_Website.Models.Enums;

/// <summary>Person-to-person relation type.</summary>
public enum RelationType
{
    Family = 0,
    Ally = 1,
    Enemy = 2,
    BusinessPartner = 3,
    Known = 4,
    Misc = 5,
}

/// <summary>Display labels.</summary>
public static class RelationTypeDisplay
{
    public static string Name(RelationType type) =>
        EnumLabelText.Get(nameof(RelationType), type.ToString()) is { } label ? label : DefaultName(type);

    /// <summary>Code-defined label, without DB override.</summary>
    public static string DefaultName(RelationType type) => type switch
    {
        RelationType.Family => "Familie",
        RelationType.Ally => "Verbündeter",
        RelationType.Enemy => "Feind",
        RelationType.BusinessPartner => "Geschäftspartner",
        RelationType.Known => "Bekannt",
        RelationType.Misc => "Sonstige",
        _ => "—",
    };

    public static readonly IReadOnlyList<RelationType> All = new[]
    {
        RelationType.Family,
        RelationType.Ally,
        RelationType.Enemy,
        RelationType.BusinessPartner,
        RelationType.Known,
        RelationType.Misc,
    };
}
