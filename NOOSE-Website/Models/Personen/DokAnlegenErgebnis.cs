namespace NOOSE_Website.Models.Personen;

/// <summary>
/// Ergebnis des übergreifenden „Neues Dok"-Dialogs: die erfassten Dok-Daten plus die Zuordnung zur
/// Person. Entweder verweist <see cref="PersonId"/> auf eine bestehende Akte, oder <see cref="NeuerName"/>
/// trägt den Namen einer noch anzulegenden Akte (genau eines von beiden ist gesetzt).
/// </summary>
public class DokAnlegenErgebnis
{
    /// <summary>Id einer bestehenden Person, falls eine ausgewählt wurde.</summary>
    public string? PersonId { get; init; }

    /// <summary>Name für eine neu anzulegende Akte, falls keine bestehende Person gewählt wurde.</summary>
    public string? NeuerName { get; init; }

    /// <summary>Die erfassten Dok-Daten.</summary>
    public PersonDokEingabe Eingabe { get; init; } = new();
}
