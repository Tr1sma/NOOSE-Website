using MudBlazor;

namespace NOOSE_Website.Services.Public;

/// <summary>The editorial page the structured FAQ is rendered into.</summary>
/// <remarks>
/// The sections have no route of their own on purpose: the FAQ is a shape given to one editorial page, so the page
/// decides whether any of it is reachable and the Information module decides whether the page is. Rename or retract
/// <c>/info/faq</c> and the accordion is gone - the editorial panel says so rather than the service reaching into
/// the page's own rules to prevent it.
/// </remarks>
public static class PublicFaq
{
    public const string PageSlug = "faq";

    /// <summary>Query parameter that tells the statically rendered page which question to open.</summary>
    /// <remarks>
    /// A fragment never reaches the server, and the page renders without a circuit, so the open state has to arrive
    /// in the query. The fragment is sent as well, for the browser to scroll with.
    /// </remarks>
    public const string OpenParameter = "frage";

    /// <summary>Icon of a section that picked none.</summary>
    public const string RubrikDefaultIcon = Icons.Material.Filled.QuestionAnswer;

    /// <summary>Address of one question: query for the server, fragment for the browser.</summary>
    public static string Href(string anchor) => $"/info/{PageSlug}?{OpenParameter}={anchor}#{anchor}";
}
