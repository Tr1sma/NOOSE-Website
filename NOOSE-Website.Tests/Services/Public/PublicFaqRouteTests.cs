using System.Runtime.CompilerServices;
using NOOSE_Website.Services.Public;

namespace NOOSE_Website.Tests.Services.Public;

/// <summary>The FAQ is a page of its own; the address it left behind still has to lead there.</summary>
/// <remarks>
/// Every deep link shared while the FAQ lived under <c>/info/faq</c> carries the question in the query, because a
/// fragment never reaches a statically rendered page. Losing either half of that on the move would break links
/// that are already in Discord, which no unit test of the service would notice.
/// </remarks>
public class PublicFaqRouteTests
{
    private static string ComponentsRoot([CallerFilePath] string here = "")
        => Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(here)!, "..", "..", "..", "NOOSE-Website", "Components"));

    private static string Read(params string[] parts)
    {
        var file = Path.Combine(new[] { ComponentsRoot() }.Concat(parts).ToArray());
        Assert.True(File.Exists(file), $"Komponente nicht gefunden: {file}");
        return File.ReadAllText(file);
    }

    [Fact]
    public void AQuestion_IsAddressedOnTheFaqsOwnRoute()
    {
        // query for the server, fragment for the browser - the page renders without a circuit
        Assert.Equal("/faq?frage=bekomme-ich-geld#bekomme-ich-geld", PublicFaq.Href("bekomme-ich-geld"));
    }

    [Fact]
    public void PageHref_SendsOnlyTheFaqSlugToTheFaqsRoute()
    {
        Assert.Equal("/faq", PublicFaq.PageHref(PublicFaq.PageSlug));
        Assert.Equal("/info/auftrag", PublicFaq.PageHref("auftrag"));
    }

    [Fact]
    public void Owns_MatchesTheSlugTheWayARouteDoes()
    {
        // stored slugs are lowercase, the public path matches case-insensitively; this decides a redirect
        Assert.True(PublicFaq.Owns("faq"));
        Assert.True(PublicFaq.Owns("FAQ"));
        Assert.False(PublicFaq.Owns("faqs"));
        Assert.False(PublicFaq.Owns(null));
    }

    [Fact]
    public void TheFaqPage_ClaimsTheRouteTheModuleNames()
    {
        var page = Read("Pages", "Public", "FaqHub.razor");

        Assert.Contains($"@page \"{PublicFaq.Route}\"", page, StringComparison.Ordinal);
        Assert.Contains("PublicModules.Faq", page, StringComparison.Ordinal);
    }

    [Fact]
    public void TheOldAddress_RedirectsInsteadOfAnsweringNotFound()
    {
        // the editorial snapshot no longer carries the row, so without this the old links would all 404
        var info = Read("Pages", "Public", "InfoPage.razor");

        Assert.Contains("PublicFaq.Owns(Slug)", info, StringComparison.Ordinal);
        Assert.Contains("Nav.NavigateTo(RedirectTarget()", info, StringComparison.Ordinal);
        // the deep link is the reason the redirect exists at all
        Assert.Contains(PublicFaq.OpenParameter, info, StringComparison.Ordinal);
    }

    [Fact]
    public void NoPublicPageStillRendersTheFaqUnderTheInformationRoute()
    {
        var info = Read("Pages", "Public", "InfoPage.razor");

        Assert.DoesNotContain("PublicFaqAccordion", info, StringComparison.Ordinal);
        Assert.DoesNotContain("IPublicFaqService", info, StringComparison.Ordinal);
    }
}
