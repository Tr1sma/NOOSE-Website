using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Data.Entities.Cases;

namespace NOOSE_Website.Models.Common;

/// <summary>Record types that allow admin-defined custom fields; keyed by CLR type name.</summary>
public static class CustomFieldRecordTypes
{
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

    /// <summary>Display name for a record type; falls back to the type name.</summary>
    public static string Display(string typeName)
        => All.FirstOrDefault(e => e.TypeName == typeName).Display ?? typeName;
}
