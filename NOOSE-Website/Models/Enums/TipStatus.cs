namespace NOOSE_Website.Models.Enums;

/// <summary>Handling state of a citizen tip.</summary>
public enum TipStatus
{
    Neu = 0,
    InPruefung = 1,
    Rueckfrage = 2,
    Bestaetigt = 3,
    Verworfen = 4,
    FuehrteZurErgreifung = 5,
}

/// <summary>Display labels.</summary>
public static class TipStatusDisplay
{
    public static string Name(TipStatus status) => status switch
    {
        TipStatus.Neu => "Neu",
        TipStatus.InPruefung => "In Prüfung",
        TipStatus.Rueckfrage => "Rückfrage",
        TipStatus.Bestaetigt => "Bestätigt",
        TipStatus.Verworfen => "Verworfen",
        TipStatus.FuehrteZurErgreifung => "Führte zur Ergreifung",
        _ => "—",
    };

    /// <summary>What the citizen is told; the internal wording would read as a verdict on the person.</summary>
    public static string CitizenName(TipStatus status) => status switch
    {
        TipStatus.Neu => "Eingegangen",
        TipStatus.InPruefung => "In Prüfung",
        TipStatus.Rueckfrage => "Rückfrage offen",
        TipStatus.Bestaetigt => "Bestätigt",
        TipStatus.Verworfen => "Abgeschlossen",
        TipStatus.FuehrteZurErgreifung => "Führte zur Ergreifung",
        _ => "—",
    };

    public static readonly IReadOnlyList<TipStatus> All = new[]
    {
        TipStatus.Neu,
        TipStatus.InPruefung,
        TipStatus.Rueckfrage,
        TipStatus.Bestaetigt,
        TipStatus.Verworfen,
        TipStatus.FuehrteZurErgreifung,
    };
}
