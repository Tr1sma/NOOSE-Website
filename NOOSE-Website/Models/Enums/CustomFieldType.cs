namespace NOOSE_Website.Models.Enums;

/// <summary>Datentyp eines admin-definierten Custom-Felds. Bestimmt das Eingabe-Steuerelement im
/// Zusatzfelder-Panel und wie der Wert (immer als String gespeichert) interpretiert wird.</summary>
public enum CustomFieldType
{
    /// <summary>Einzeiliger Text.</summary>
    Text = 0,

    /// <summary>Mehrzeiliger Text.</summary>
    Multiline = 1,

    /// <summary>Zahl (ganz/dezimal, Invariant-Kultur gespeichert).</summary>
    Number = 2,

    /// <summary>Datum (als ISO <c>yyyy-MM-dd</c> gespeichert).</summary>
    Date = 3,

    /// <summary>Ja/Nein (als <c>true</c>/<c>false</c> gespeichert).</summary>
    YesNo = 4,

    /// <summary>Auswahl aus vorgegebenen Optionen (siehe <c>CustomFeldDefinition.Optionen</c>).</summary>
    Selection = 5,
}

/// <summary>Anzeigetexte für den Custom-Feld-Typ (UI-frei, ohne MudBlazor-Abhängigkeit).</summary>
public static class CustomFieldTypeDisplay
{
    public static string Name(CustomFieldType type) => type switch
    {
        CustomFieldType.Text => "Text",
        CustomFieldType.Multiline => "Text (mehrzeilig)",
        CustomFieldType.Number => "Zahl",
        CustomFieldType.Date => "Datum",
        CustomFieldType.YesNo => "Ja/Nein",
        CustomFieldType.Selection => "Auswahl",
        _ => "—",
    };

    public static readonly IReadOnlyList<CustomFieldType> All = new[]
    {
        CustomFieldType.Text,
        CustomFieldType.Multiline,
        CustomFieldType.Number,
        CustomFieldType.Date,
        CustomFieldType.YesNo,
        CustomFieldType.Selection,
    };
}
