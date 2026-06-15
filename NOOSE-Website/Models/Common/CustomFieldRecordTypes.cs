using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Data.Entities.Cases;

namespace NOOSE_Website.Models.Common;

/// <summary>
/// Registry der Aktentypen, für die admin-definierte Custom-Felder (Zusatzfelder) erlaubt sind.
/// Liefert für die Admin-Auswahl den CLR-Typnamen (<c>nameof</c>, identisch zur polymorphen
/// <c>EntitaetTyp</c>-Konvention) samt deutschem Anzeigenamen.
/// </summary>
public static class CustomFieldRecordTypes
{
    /// <summary>Ein unterstützter Aktentyp: <paramref name="TypName"/> = CLR-Typname (für EntitaetTyp),
    /// <paramref name="Anzeige"/> = deutscher Anzeigename.</summary>
    public readonly record struct Entry(string TypeName, string Display);

    public static readonly IReadOnlyList<Entry> All = new[]
    {
        new Entry(nameof(Person), "Person"),
        new Entry(nameof(Faction), "Fraktion"),
        new Entry(nameof(PersonGroup), "Personengruppe"),
        new Entry(nameof(Party), "Partei"),
        new Entry(nameof(Operation), "Operation"),
        new Entry(nameof(Case), "Vorgang"),
        new Entry(nameof(Taskforce), "Taskforce"),
    };

    /// <summary>Deutscher Anzeigename eines Aktentyps; fällt auf den Typnamen zurück.</summary>
    public static string Display(string typeName)
        => All.FirstOrDefault(e => e.TypeName == typeName).Display ?? typeName;
}
