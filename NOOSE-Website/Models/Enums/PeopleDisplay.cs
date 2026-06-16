namespace NOOSE_Website.Models.Enums;

/// <summary>Display labels.</summary>
public static class LifeStatusDisplay
{
    public static string Name(LifeStatus status) => status switch
    {
        LifeStatus.Alive => "Lebend",
        LifeStatus.Dead => "Tot",
        LifeStatus.Fugitive => "Flüchtig",
        _ => "—",
    };

    public static readonly IReadOnlyList<LifeStatus> All = new[]
    {
        LifeStatus.Alive,
        LifeStatus.Dead,
        LifeStatus.Fugitive,
    };
}

/// <summary>Display labels.</summary>
public static class ClassificationDisplay
{
    public static string Name(Classification classification) => classification switch
    {
        Classification.Unknown => "Unbekannt",
        Classification.ReviewCase => "Prüffall",
        Classification.SuspicionCase => "Verdachtsfall",
        Classification.SecuredStateThreatening => "Gesichert staatsgefährdend",
        _ => "—",
    };

    public static readonly IReadOnlyList<Classification> All = new[]
    {
        Classification.Unknown,
        Classification.ReviewCase,
        Classification.SuspicionCase,
        Classification.SecuredStateThreatening,
    };
}

/// <summary>Display labels.</summary>
public static class MeasureOutcomeDisplay
{
    public static string Name(MeasureOutcome outcome) => outcome switch
    {
        MeasureOutcome.RunningStill => "Läuft noch",
        MeasureOutcome.OfficiallyReleased => "Offiziell entlassen",
        MeasureOutcome.Injection => "Amnestie-Spritze",
        MeasureOutcome.Shot => "Erschossen",
        _ => "—",
    };

    public static readonly IReadOnlyList<MeasureOutcome> All = new[]
    {
        MeasureOutcome.RunningStill,
        MeasureOutcome.OfficiallyReleased,
        MeasureOutcome.Injection,
        MeasureOutcome.Shot,
    };
}
