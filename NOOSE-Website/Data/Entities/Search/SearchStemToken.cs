using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Search;

/// <summary>Persisted German word stem for a record's searchable text. Polymorphic (no FK), append-only, no markers —
/// a pure derived side-index rebuilt by the search interceptor. SourceId is the origin row (delete key).</summary>
[Table("Suche_WortStaemme")]
public class SearchStemToken
{
    public long Id { get; set; }

    [Column("EntitaetTyp")]
    public string EntityType { get; set; } = string.Empty;

    [Column("EntitaetId")]
    public string EntityId { get; set; } = string.Empty;

    [Column("QuelleId")]
    public string SourceId { get; set; } = string.Empty;

    [Column("Stamm")]
    public string Stem { get; set; } = string.Empty;
}
