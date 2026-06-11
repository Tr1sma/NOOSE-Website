using NOOSE_Website.Data.Entities.Fraktionen;
using NOOSE_Website.Data.Entities.Gruppen;
using NOOSE_Website.Data.Entities.Operationen;
using NOOSE_Website.Data.Entities.Parteien;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Data.Entities.Vorgaenge;

namespace NOOSE_Website.Models.Querschnitt;

/// <summary>
/// Registry der Aktentypen, für die admin-definierte Custom-Felder (Zusatzfelder) erlaubt sind.
/// Liefert für die Admin-Auswahl den CLR-Typnamen (<c>nameof</c>, identisch zur polymorphen
/// <c>EntitaetTyp</c>-Konvention) samt deutschem Anzeigenamen.
/// </summary>
public static class CustomFeldAktentypen
{
    /// <summary>Ein unterstützter Aktentyp: <paramref name="TypName"/> = CLR-Typname (für EntitaetTyp),
    /// <paramref name="Anzeige"/> = deutscher Anzeigename.</summary>
    public readonly record struct Eintrag(string TypName, string Anzeige);

    public static readonly IReadOnlyList<Eintrag> Alle = new[]
    {
        new Eintrag(nameof(Person), "Person"),
        new Eintrag(nameof(Fraktion), "Fraktion"),
        new Eintrag(nameof(Personengruppe), "Personengruppe"),
        new Eintrag(nameof(Partei), "Partei"),
        new Eintrag(nameof(Operation), "Operation"),
        new Eintrag(nameof(Vorgang), "Vorgang"),
        new Eintrag(nameof(Taskforce), "Taskforce"),
    };

    /// <summary>Deutscher Anzeigename eines Aktentyps; fällt auf den Typnamen zurück.</summary>
    public static string Anzeige(string typName)
        => Alle.FirstOrDefault(e => e.TypName == typName).Anzeige ?? typName;
}
