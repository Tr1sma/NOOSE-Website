using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.People;

/// <summary>
/// Formular-/Eingabemodell zum Anlegen und Bearbeiten einer Person. Die Mehrfach-Felder des
/// Steckbriefs sind Listen kleiner Eingabe-Objekte (stabile Referenzen für die Inline-Bearbeitung).
/// </summary>
public class PersonInput
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public LifeStatus LifeStatus { get; set; } = LifeStatus.Alive;
    public Classification Classification { get; set; } = Classification.Unknown;
    public string? ClassificationJustification { get; set; }
    public bool IsClassified { get; set; }

    public List<AliasInput> Aliases { get; set; } = new();
    public List<PhoneInput> PhoneNumbers { get; set; } = new();
    public List<VehicleInput> Vehicles { get; set; } = new();
    public List<LocationInput> Locations { get; set; } = new();
    public List<WeaponInput> Weapons { get; set; } = new();
}

/// <summary>
/// Gemeinsame Sicht auf ein Steckbrief-Mehrfachfeld: ein Hauptwert plus optionaler Zusatz. Erlaubt der
/// generischen Chip-Eingabe (<c>SteckbriefMehrfachFeld</c>), einheitlich auf die unterschiedlich
/// benannten Felder von Waffe/Fahrzeug/Ort zuzugreifen. Implementiert wird das Interface explizit,
/// damit die Originalfelder (Text/Bezeichnung/Notiz/Kennzeichen) für Persistenz und Lese-Ansicht erhalten bleiben.
/// </summary>
public interface IProfileMultiple
{
    string MainValue { get; set; }
    string? Extra { get; set; }
}

public class AliasInput
{
    public string AliasName { get; set; } = string.Empty;
}

public class PhoneInput
{
    public string Number { get; set; } = string.Empty;
    public string? Designation { get; set; }
}

public class VehicleInput : IProfileMultiple
{
    public string Designation { get; set; } = string.Empty;
    public string? LicensePlate { get; set; }

    string IProfileMultiple.MainValue { get => Designation; set => Designation = value; }
    string? IProfileMultiple.Extra { get => LicensePlate; set => LicensePlate = value; }
}

public class LocationInput : IProfileMultiple
{
    public string Text { get; set; } = string.Empty;
    public string? Note { get; set; }

    string IProfileMultiple.MainValue { get => Text; set => Text = value; }
    string? IProfileMultiple.Extra { get => Note; set => Note = value; }
}

public class WeaponInput : IProfileMultiple
{
    public string Text { get; set; } = string.Empty;

    string IProfileMultiple.MainValue { get => Text; set => Text = value; }
    // Waffen haben kein Zusatzfeld.
    string? IProfileMultiple.Extra { get => null; set { } }
}
