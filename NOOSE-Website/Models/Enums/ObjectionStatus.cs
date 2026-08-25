namespace NOOSE_Website.Models.Enums;

/// <summary>Handling state of a citizen's objection to a public wanted notice.</summary>
public enum ObjectionStatus
{
    Neu = 0,
    InPruefung = 1,
    Angenommen = 2,
    Abgelehnt = 3,
}

/// <summary>Display labels.</summary>
public static class ObjectionStatusDisplay
{
    public static string Name(ObjectionStatus status) => status switch
    {
        ObjectionStatus.Neu => "Neu",
        ObjectionStatus.InPruefung => "In Prüfung",
        ObjectionStatus.Angenommen => "Stattgegeben",
        ObjectionStatus.Abgelehnt => "Abgelehnt",
        _ => "—",
    };

    /// <summary>What the citizen is told; "Neu" describes the desk's queue, not their submission.</summary>
    public static string CitizenName(ObjectionStatus status) => status switch
    {
        ObjectionStatus.Neu => "Eingegangen",
        ObjectionStatus.InPruefung => "In Prüfung",
        ObjectionStatus.Angenommen => "Stattgegeben",
        ObjectionStatus.Abgelehnt => "Zurückgewiesen",
        _ => "—",
    };

    public static readonly IReadOnlyList<ObjectionStatus> All = new[]
    {
        ObjectionStatus.Neu,
        ObjectionStatus.InPruefung,
        ObjectionStatus.Angenommen,
        ObjectionStatus.Abgelehnt,
    };
}
