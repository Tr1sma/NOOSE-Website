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

/// <summary>
/// Gemeinsame Sicht auf ein Steckbrief-Mehrfachfeld: ein Hauptwert plus optionaler Zusatz. Erlaubt der
/// generischen Chip-Eingabe (<c>SteckbriefMehrfachFeld</c>), einheitlich auf die unterschiedlich
/// benannten Felder von Waffe/Fahrzeug/Ort zuzugreifen. Implementiert wird das Interface explizit,
/// damit die Originalfelder (Text/Bezeichnung/Notiz/Kennzeichen) für Persistenz und Lese-Ansicht erhalten bleiben.
/// </summary>
public interface ISteckbriefMehrfach
{
    string Hauptwert { get; set; }
    string? Zusatz { get; set; }
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

public class FahrzeugEingabe : ISteckbriefMehrfach
{
    public string Bezeichnung { get; set; } = string.Empty;
    public string? Kennzeichen { get; set; }

    string ISteckbriefMehrfach.Hauptwert { get => Bezeichnung; set => Bezeichnung = value; }
    string? ISteckbriefMehrfach.Zusatz { get => Kennzeichen; set => Kennzeichen = value; }
}

public class OrtEingabe : ISteckbriefMehrfach
{
    public string Text { get; set; } = string.Empty;
    public string? Notiz { get; set; }

    string ISteckbriefMehrfach.Hauptwert { get => Text; set => Text = value; }
    string? ISteckbriefMehrfach.Zusatz { get => Notiz; set => Notiz = value; }
}

public class WaffeEingabe : ISteckbriefMehrfach
{
    public string Text { get; set; } = string.Empty;

    string ISteckbriefMehrfach.Hauptwert { get => Text; set => Text = value; }
    // Waffen haben kein Zusatzfeld.
    string? ISteckbriefMehrfach.Zusatz { get => null; set { } }
}
