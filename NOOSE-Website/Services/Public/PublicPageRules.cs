namespace NOOSE_Website.Services.Public;

/// <summary>Length limits of an editorial page's short fields. The service and the panel read them here.</summary>
/// <remarks>
/// The service cuts to these lengths rather than refusing, because the columns hold exactly this much and MySQL
/// would truncate a longer value without a word. That makes the panel's own <c>MaxLength</c> the only place a
/// person is told where the limit is - the same reason <see cref="PublicWantedRules"/> exists. The address has its
/// own limit in <see cref="PublicPageSlug"/>, which is refused rather than cut.
/// </remarks>
public static class PublicPageRules
{
    public const int MaxTitle = 200;
    public const int MaxMenuTitle = 64;
}
