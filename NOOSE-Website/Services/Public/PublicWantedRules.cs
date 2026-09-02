namespace NOOSE_Website.Services.Public;

/// <summary>Length limits of the outward fields of a wanted notice. The service and the editor read them here.</summary>
/// <remarks>
/// The service cuts to these lengths rather than refusing, because the columns hold exactly this much and MySQL
/// would truncate a longer value without a word. That makes the editor's own <c>MaxLength</c> the only place a
/// person is told where the limit is: without it the name on an anonymous poster could come out shortened and
/// nobody would learn of it.
/// </remarks>
public static class PublicWantedRules
{
    public const int MaxDisplayName = 200;
    public const int MaxAliasText = 400;
    public const int MaxLastArea = 200;
    public const int MaxVehicleText = 400;
}
