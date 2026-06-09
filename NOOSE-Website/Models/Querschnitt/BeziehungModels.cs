using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Querschnitt;

/// <summary>Eingabe zum Anlegen einer Person-zu-Person-Beziehung.</summary>
public class BeziehungEingabe
{
    public string ZielPersonId { get; set; } = string.Empty;
    public BeziehungsTyp Typ { get; set; } = BeziehungsTyp.Bekannt;
    public string? Notiz { get; set; }
}

/// <summary>Aufbereitete Beziehung aus Sicht einer Person: die jeweils andere Person + Typ.</summary>
public record BeziehungAnzeige(
    string BeziehungId,
    BeziehungsTyp Typ,
    string? Notiz,
    string AnderePersonId,
    string AnderePersonName,
    string AnderePersonAktenzeichen);
