namespace NOOSE_Website.Models.Enums;

/// <summary>Art einer Person-zu-Person-Beziehung.</summary>
public enum BeziehungsTyp
{
    Familie = 0,
    Verbuendeter = 1,
    Feind = 2,
    Geschaeftspartner = 3,
    Bekannt = 4,
    Sonstige = 5,
}

/// <summary>Anzeigetexte für den Beziehungstyp (UI-frei, ohne MudBlazor-Abhängigkeit).</summary>
public static class BeziehungsTypAnzeige
{
    public static string Name(BeziehungsTyp typ) => typ switch
    {
        BeziehungsTyp.Familie => "Familie",
        BeziehungsTyp.Verbuendeter => "Verbündeter",
        BeziehungsTyp.Feind => "Feind",
        BeziehungsTyp.Geschaeftspartner => "Geschäftspartner",
        BeziehungsTyp.Bekannt => "Bekannt",
        BeziehungsTyp.Sonstige => "Sonstige",
        _ => "—",
    };

    public static readonly IReadOnlyList<BeziehungsTyp> Alle = new[]
    {
        BeziehungsTyp.Familie,
        BeziehungsTyp.Verbuendeter,
        BeziehungsTyp.Feind,
        BeziehungsTyp.Geschaeftspartner,
        BeziehungsTyp.Bekannt,
        BeziehungsTyp.Sonstige,
    };
}
