using MudBlazor;

namespace NOOSE_Website.Services.Public;

/// <summary>Where the structured FAQ lives and how one question is addressed.</summary>
/// <remarks>
/// The FAQ is a page of its own under <see cref="Route"/> with a module switch of its own, but it keeps taking its
/// heading, its intro text and its published state from the editorial row with <see cref="PageSlug"/>. That row is
/// therefore held out of the Information menu and out of <c>/info/&lt;slug&gt;</c>: one address per piece of
/// content, so a visitor cannot reach the same text twice and the public search cannot offer it twice. Retract the
/// row and the FAQ is gone - the editorial panel says so rather than the service reaching into the page's rules.
/// </remarks>
public static class PublicFaq
{
    public const string PageSlug = "faq";

    /// <summary>The FAQ's own address; the module names the same route, so the crawler and the nav agree.</summary>
    public const string Route = "/faq";

    /// <summary>The address the FAQ answered on before it became a page of its own.</summary>
    public const string LegacyRoute = "/info/" + PageSlug;

    /// <summary>Query parameter that tells the statically rendered page which question to open.</summary>
    /// <remarks>
    /// A fragment never reaches the server, and the page renders without a circuit, so the open state has to arrive
    /// in the query. The fragment is sent as well, for the browser to scroll with.
    /// </remarks>
    public const string OpenParameter = "frage";

    /// <summary>Icon of a section that picked none.</summary>
    public const string RubrikDefaultIcon = Icons.Material.Filled.QuestionAnswer;

    /// <summary>Address of one question: query for the server, fragment for the browser.</summary>
    public static string Href(string anchor) => $"{Route}?{OpenParameter}={anchor}#{anchor}";

    /// <summary>True for the editorial slug the FAQ owns; that row is not an Information page any more.</summary>
    public static bool Owns(string? slug) => string.Equals(slug, PageSlug, StringComparison.OrdinalIgnoreCase);

    /// <summary>Public address of an editorial page - the FAQ's own route for the row it owns.</summary>
    /// <remarks>
    /// Every editorial link goes through here rather than composing <c>/info/{slug}</c>, because exactly one row
    /// answers somewhere else and a hand-built link to it would land on a redirect at best.
    /// </remarks>
    public static string PageHref(string slug) => Owns(slug) ? Route : $"/info/{slug}";
}
