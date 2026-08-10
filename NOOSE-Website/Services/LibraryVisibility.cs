using NOOSE_Website.Authorization;
using NOOSE_Website.Data.Entities.Common;

namespace NOOSE_Website.Services;

/// <summary>Library file visibility: the secrecy level and nothing else. Partners have no library at all.</summary>
public static class LibraryVisibility
{
    /// <summary>Files the viewer may see. Filters on the three stored bools rather than the derived
    /// <see cref="LibraryFile.Classification"/>, which is not mapped and cannot be translated to SQL.</summary>
    public static IQueryable<LibraryFile> OnlyVisible(this IQueryable<LibraryFile> query, DocumentViewerScope scope)
    {
        // locals so EF parameterizes the classification filters
        bool mayClassified = scope.MayClassified, isTru = scope.IsTru, isHrb = scope.IsHrb;
        return query.Where(d => (!d.IsClassified && !d.IsTRUClassified && !d.IsHRBClassified)
            || mayClassified
            || (d.IsTRUClassified && isTru)
            || (d.IsHRBClassified && isHrb));
    }
}
