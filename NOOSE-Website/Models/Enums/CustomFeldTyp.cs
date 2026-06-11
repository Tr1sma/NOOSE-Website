namespace NOOSE_Website.Models.Enums;

/// <summary>Datentyp eines admin-definierten Custom-Felds. Bestimmt das Eingabe-Steuerelement im
/// Zusatzfelder-Panel und wie der Wert (immer als String gespeichert) interpretiert wird.</summary>
public enum CustomFeldTyp
{
    /// <summary>Einzeiliger Text.</summary>
    Text = 0,

    /// <summary>Mehrzeiliger Text.</summary>
    Mehrzeilig = 1,

    /// <summary>Zahl (ganz/dezimal, Invariant-Kultur gespeichert).</summary>
    Zahl = 2,

    /// <summary>Datum (als ISO <c>yyyy-MM-dd</c> gespeichert).</summary>
    Datum = 3,

    /// <summary>Ja/Nein (als <c>true</c>/<c>false</c> gespeichert).</summary>
    JaNein = 4,

    /// <summary>Auswahl aus vorgegebenen Optionen (siehe <c>CustomFeldDefinition.Optionen</c>).</summary>
    Auswahl = 5,
}

/// <summary>Anzeigetexte für den Custom-Feld-Typ (UI-frei, ohne MudBlazor-Abhängigkeit).</summary>
public static class CustomFeldTypAnzeige
{
    public static string Name(CustomFeldTyp typ) => typ switch
    {
        CustomFeldTyp.Text => "Text",
        CustomFeldTyp.Mehrzeilig => "Text (mehrzeilig)",
        CustomFeldTyp.Zahl => "Zahl",
        CustomFeldTyp.Datum => "Datum",
        CustomFeldTyp.JaNein => "Ja/Nein",
        CustomFeldTyp.Auswahl => "Auswahl",
        _ => "—",
    };

    public static readonly IReadOnlyList<CustomFeldTyp> Alle = new[]
    {
        CustomFeldTyp.Text,
        CustomFeldTyp.Mehrzeilig,
        CustomFeldTyp.Zahl,
        CustomFeldTyp.Datum,
        CustomFeldTyp.JaNein,
        CustomFeldTyp.Auswahl,
    };
}
