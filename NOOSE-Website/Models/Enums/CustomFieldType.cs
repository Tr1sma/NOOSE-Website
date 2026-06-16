namespace NOOSE_Website.Models.Enums;

/// <summary>Custom field data type.</summary>
public enum CustomFieldType
{
    /// <summary>Single-line text.</summary>
    Text = 0,

    /// <summary>Multi-line text.</summary>
    Multiline = 1,

    /// <summary>Numeric value.</summary>
    Number = 2,

    /// <summary>Date (ISO yyyy-MM-dd).</summary>
    Date = 3,

    /// <summary>Yes/No boolean.</summary>
    YesNo = 4,

    /// <summary>Predefined options list.</summary>
    Selection = 5,
}

/// <summary>Display labels.</summary>
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
