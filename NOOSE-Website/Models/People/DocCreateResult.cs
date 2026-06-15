namespace NOOSE_Website.Models.People;

/// <summary>
/// Ergebnis des übergreifenden „Neues Dok"-Dialogs: die erfassten Dok-Daten plus die Zuordnung zur
/// Person. Entweder verweist <see cref="PersonId"/> auf eine bestehende Akte, oder <see cref="NeuerName"/>
/// trägt den Namen einer noch anzulegenden Akte (genau eines von beiden ist gesetzt).
/// </summary>
public class DocCreateResult
{
    /// <summary>Id einer bestehenden Person, falls eine ausgewählt wurde.</summary>
    public string? PersonId { get; init; }

    /// <summary>Name für eine neu anzulegende Akte, falls keine bestehende Person gewählt wurde.</summary>
    public string? NewName { get; init; }

    /// <summary>Die erfassten Dok-Daten.</summary>
    public PersonDocInput Input { get; init; } = new();
}
