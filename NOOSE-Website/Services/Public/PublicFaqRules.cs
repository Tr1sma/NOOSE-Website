namespace NOOSE_Website.Services.Public;

/// <summary>Limits of the public FAQ. The service, the panel and the tests all read them here.</summary>
/// <remarks>
/// The two length limits are cut rather than refused, because the columns hold exactly this much and MySQL would
/// truncate a longer value without a word - the same reason <see cref="PublicPageRules"/> exists.
/// <para>
/// The two counts are refused, and they are not cosmetic: a closed <c>&lt;details&gt;</c> still ships its content, so
/// every answer of every section is in the response of every anonymous visit. With pictures pasted as base64 that
/// grows without anyone noticing, which is why the cap sits next to the lengths instead of in the panel.
/// </para>
/// </remarks>
public static class PublicFaqRules
{
    public const int MaxTitle = 160;
    public const int MaxDescription = 400;
    public const int MaxQuestion = 300;

    public const int MaxRubriken = 20;
    public const int MaxEntriesPerRubrik = 40;
}
