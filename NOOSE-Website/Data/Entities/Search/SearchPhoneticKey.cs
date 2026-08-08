using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Search;

/// <summary>Persisted Cologne-phonetic key for a record name/alias. Polymorphic (no FK), append-only, no markers —
/// a pure derived side-index rebuilt by the search interceptor. SourceId is the origin row (delete key).</summary>
[Table("Suche_PhonetikSchluessel")]
public class SearchPhoneticKey
{
    public long Id { get; set; }

    [Column("EntitaetTyp")]
    public string EntityType { get; set; } = string.Empty;

    [Column("EntitaetId")]
    public string EntityId { get; set; } = string.Empty;

    [Column("QuelleId")]
    public string SourceId { get; set; } = string.Empty;

    [Column("Schluessel")]
    public string Key { get; set; } = string.Empty;
}
