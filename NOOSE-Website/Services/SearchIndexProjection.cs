using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.Jobs;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Taskforces;

namespace NOOSE_Website.Services;

/// <summary>The phonetic keys + stems a record contributes to the search side-index, keyed by the target record
/// (aliases fold onto their person) and the origin row (SourceId, the delete key).</summary>
public readonly record struct SearchIndexRow(
    string EntityType, string EntityId, string SourceId,
    IReadOnlyList<string> PhoneticKeys, IReadOnlyList<string> Stems);

/// <summary>Central mapping of search-indexed entities to their phonetic seeds (name fields) and stem fields (all text).
/// Kept in one place instead of a marker interface so entity classes stay clean and EF-agnostic.</summary>
public static class SearchIndexProjection
{
    public static readonly IReadOnlySet<Type> IndexedTypes = new HashSet<Type>
    {
        typeof(Person), typeof(PersonAlias), typeof(Faction), typeof(PersonGroup),
        typeof(Party), typeof(Operation), typeof(Taskforce), typeof(Case), typeof(Job),
    };

    /// <summary>Index contribution for an entity, or null when the type is not search-indexed.</summary>
    public static SearchIndexRow? For(object entity) => entity switch
    {
        Person p => Build(nameof(Person), p.Id, p.Id,
            new[] { p.Name }, new[] { p.Name, p.Description, p.WantedReason, p.CaseNumber }),
        // aliases pay into their person's index (target = PersonId, source = alias id)
        PersonAlias a => Build(nameof(Person), a.PersonId, a.Id,
            new[] { a.AliasName }, new[] { a.AliasName }),
        Faction f => Build(nameof(Faction), f.Id, f.Id,
            new[] { f.Name },
            new[] { f.Name, f.Kind, f.Radio, f.Darkchat, f.IssuingTimes, f.Estate, f.Targets, f.Description, f.CaseNumber }),
        PersonGroup g => Build(nameof(PersonGroup), g.Id, g.Id,
            new[] { g.Name }, new[] { g.Name, g.Description, g.Targets, g.CaseNumber }),
        Party p => Build(nameof(Party), p.Id, p.Id,
            new[] { p.Name }, new[] { p.Name, p.Description, p.Targets, p.Remarks, p.CaseNumber }),
        Operation o => Build(nameof(Operation), o.Id, o.Id,
            new[] { o.Title }, new[] { o.Title, o.Type, o.Location, o.Expiry, o.Result, o.Remarks, o.CaseNumber }),
        Taskforce t => Build(nameof(Taskforce), t.Id, t.Id,
            new[] { t.Name }, new[] { t.Name, t.Purpose, t.Remarks, t.CaseNumber }),
        Case c => Build(nameof(Case), c.Id, c.Id,
            new[] { c.Title }, new[] { c.Title, c.Type, c.Description, c.Summary, c.ClosingNote, c.CaseNumber }),
        Job j => Build(nameof(Job), j.Id, j.Id,
            new[] { j.Title }, new[] { j.Title, j.Description, j.CaseNumber }),
        _ => null,
    };

    private static SearchIndexRow Build(string type, string id, string sourceId, string?[] names, string?[] texts)
        => new(type, id, sourceId, SearchTokenizer.PhoneticKeys(names), SearchTokenizer.Stems(texts));
}
