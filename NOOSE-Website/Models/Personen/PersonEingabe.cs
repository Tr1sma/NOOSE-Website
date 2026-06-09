using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Personen;

/// <summary>
/// Formular-/Eingabemodell zum Anlegen und Bearbeiten einer Person. Die Mehrfach-Felder des
/// Steckbriefs sind Listen kleiner Eingabe-Objekte (stabile Referenzen für die Inline-Bearbeitung).
/// </summary>
public class PersonEingabe
{
    public string Name { get; set; } = string.Empty;
    public string? Beschreibung { get; set; }
    public Lebensstatus Lebensstatus { get; set; } = Lebensstatus.Lebend;
    public Einstufung Einstufung { get; set; } = Einstufung.Unbekannt;
    public string? EinstufungBegruendung { get; set; }
    public bool IstVerschlusssache { get; set; }

    public List<AliasEingabe> Aliase { get; set; } = new();
    public List<TelefonEingabe> Telefonnummern { get; set; } = new();
    public List<FahrzeugEingabe> Fahrzeuge { get; set; } = new();
    public List<OrtEingabe> Orte { get; set; } = new();
    public List<WaffeEingabe> Waffen { get; set; } = new();
}

public class AliasEingabe
{
    public string Aliasname { get; set; } = string.Empty;
}

public class TelefonEingabe
{
    public string Nummer { get; set; } = string.Empty;
    public string? Bezeichnung { get; set; }
}

public class FahrzeugEingabe
{
    public string Bezeichnung { get; set; } = string.Empty;
    public string? Kennzeichen { get; set; }
}

public class OrtEingabe
{
    public string Text { get; set; } = string.Empty;
    public string? Notiz { get; set; }
}

public class WaffeEingabe
{
    public string Text { get; set; } = string.Empty;
}
