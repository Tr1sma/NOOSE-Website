namespace NOOSE_Website.Models.Enums;

/// <summary>Generic link kind.</summary>
public enum LinkKind
{
    Default = 0,
    Conflict = 1,
    Alliance = 2,
}

/// <summary>Display labels.</summary>
public static class LinkKindDisplay
{
    public static string Name(LinkKind kind) =>
        EnumLabelText.Get(nameof(LinkKind), kind.ToString()) is { } label ? label : DefaultName(kind);

    /// <summary>Code-defined label, without DB override.</summary>
    public static string DefaultName(LinkKind kind) => kind switch
    {
        LinkKind.Default => "Verknüpfung",
        LinkKind.Conflict => "Konflikt",
        LinkKind.Alliance => "Bündnis",
        _ => "—",
    };

    public static readonly IReadOnlyList<LinkKind> All = new[]
    {
        LinkKind.Default,
        LinkKind.Conflict,
        LinkKind.Alliance,
    };
}
