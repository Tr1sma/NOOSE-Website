using MudBlazor;
using NOOSE_Website.Services.Search;

namespace NOOSE_Website.Services;

/// <summary>German label and icon for a record's CLR type name. Used wherever a polymorphic reference is shown.</summary>
/// <remarks>
/// A façade over <see cref="SearchCatalog"/>, not a second table. The catalog already carries the German singular,
/// the plural and the icon of every record type, and CLAUDE.md names it the single truth for exactly those. The
/// link panels used to keep their own copies; eight of them drifted apart, three were missing types outright and
/// rendered the raw CLR name as a group heading.
/// </remarks>
public static class RecordTypeDisplay
{
    /// <summary>Label for the type, or the raw type name when unknown.</summary>
    public static string Name(string recordType)
        => SearchCatalog.Find(recordType)?.German ?? recordType;

    /// <summary>Plural label for the type, or the raw type name when unknown.</summary>
    public static string Plural(string recordType)
        => SearchCatalog.Find(recordType)?.Plural ?? recordType;

    /// <summary>Icon for the type; a neutral hub for anything the catalog does not know.</summary>
    public static string Icon(string recordType)
        => SearchCatalog.Find(recordType)?.Icon ?? Icons.Material.Filled.Hub;
}
