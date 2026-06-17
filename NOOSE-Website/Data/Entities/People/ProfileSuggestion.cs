using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.People;

/// <summary>Shared autocomplete value for a profile multi-field; unique per (type, value) via case-insensitive collation.</summary>
[Table("SteckbriefVorschlaege")]
public class ProfileSuggestion
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    [Column("Typ")]
    public SuggestionType Type { get; set; }
    [Column("Wert")]
    public string Value { get; set; } = string.Empty;
}
