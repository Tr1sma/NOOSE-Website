namespace NOOSE_Website.Models.Enums;

/// <summary>Document access classification.</summary>
public enum DocumentClassification
{
    /// <summary>Visible to all agents.</summary>
    None = 0,

    /// <summary>Leadership only.</summary>
    Leadership = 1,

    /// <summary>TRU unit only.</summary>
    Tru = 2,

    /// <summary>HRB unit only.</summary>
    Hrb = 3,
}

/// <summary>Display labels.</summary>
public static class DocumentClassificationDisplay
{
    /// <summary>Full classification label.</summary>
    public static string Label(DocumentClassification classification) => classification switch
    {
        DocumentClassification.Leadership => "Verschlusssache nur für Führung",
        DocumentClassification.Tru => "Verschlusssache nur für TRU",
        DocumentClassification.Hrb => "Verschlusssache nur für HRB",
        _ => "Keine Verschlusssache",
    };

    /// <summary>Short chip label.</summary>
    public static string ChipLabel(DocumentClassification classification) => classification switch
    {
        DocumentClassification.Leadership => "Verschlusssache",
        DocumentClassification.Tru => "VS – TRU",
        DocumentClassification.Hrb => "VS – HRB",
        _ => string.Empty,
    };
}
