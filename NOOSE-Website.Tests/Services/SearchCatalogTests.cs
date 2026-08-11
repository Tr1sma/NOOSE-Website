using NOOSE_Website.Data;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Services.Search;
using NOOSE_Website.Tests.Infrastructure;

namespace NOOSE_Website.Tests.Services;

/// <summary>The catalog is the single source for labels, icons, routes and capabilities. These assertions are the
/// reason a new category cannot be half-added.</summary>
public class SearchCatalogTests
{
    [Fact]
    public void Every_category_names_a_real_entity_type()
    {
        var known = typeof(AppDbContext).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Select(t => t.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Empty(SearchCatalog.Categories.Where(c => !known.Contains(c.Clr)).Select(c => c.Clr));
    }

    [Fact]
    public void Category_keys_are_unique()
    {
        var duplicates = SearchCatalog.Categories.GroupBy(c => c.Clr, StringComparer.Ordinal)
            .Where(g => g.Count() > 1).Select(g => g.Key);

        Assert.Empty(duplicates);
    }

    [Fact]
    public void German_labels_are_present_and_unique()
    {
        // materialized so a failure names the offending category instead of just saying "not empty"
        var unlabelled = SearchCatalog.Categories
            .Where(c => string.IsNullOrWhiteSpace(c.German) || string.IsNullOrWhiteSpace(c.Plural))
            .Select(c => c.Clr)
            .ToArray();
        Assert.Empty(unlabelled);

        var duplicates = SearchCatalog.Categories.GroupBy(c => c.Plural, StringComparer.Ordinal)
            .Where(g => g.Count() > 1).Select(g => g.Key);
        Assert.Empty(duplicates);
    }

    [Fact]
    public void Every_route_template_carries_the_id_placeholder()
    {
        var broken = SearchCatalog.Categories
            .Where(c => c.RouteTemplate is { } t && !t.Contains("{0}", StringComparison.Ordinal))
            .Select(c => c.Clr);

        Assert.Empty(broken);
    }

    [Fact]
    public void A_content_child_has_no_route_of_its_own()
    {
        // its hit carries the parent's type and id, so a route here would compete with the parent's
        foreach (var category in SearchCatalog.Categories.Where(c => c.Has(SearchTraits.ContentChild)))
        {
            Assert.Null(category.RouteTemplate);
        }
    }

    [Fact]
    public void A_content_child_that_lives_in_a_section_names_it()
    {
        // a child that IS a reference to the record (a watchlist entry) lands on the record itself and has none
        var withoutSection = SearchCatalog.Categories
            .Where(c => c.Has(SearchTraits.ContentChild) && !c.Has(SearchTraits.Personal))
            .Where(c => string.IsNullOrWhiteSpace(c.ParentTab))
            .Select(c => c.Clr);

        Assert.Empty(withoutSection);
    }

    [Fact]
    public void Only_a_content_child_declares_a_parent_section()
    {
        var stray = SearchCatalog.Categories
            .Where(c => c.ParentTab is not null && !c.Has(SearchTraits.ContentChild))
            .Select(c => c.Clr);

        Assert.Empty(stray);
    }

    [Fact]
    public void A_palette_category_has_a_page_to_navigate_to()
    {
        // the palette's whole job is "take me to the record I mean"; an entry with nowhere to go is not one
        foreach (var category in SearchCatalog.Categories.Where(c => c.Has(SearchTraits.Quick)))
        {
            Assert.NotNull(category.RouteTemplate);
        }
    }

    [Fact]
    public void A_content_child_is_never_offered_in_the_palette()
    {
        // Quick and Heavy are orthogonal — QuickAsync is a separate identifier-only query, so a category whose
        // full search reads longtext may still be in the palette. A hit without its own page may not.
        var offenders = SearchCatalog.Categories
            .Where(c => c.Has(SearchTraits.Quick) && c.Has(SearchTraits.ContentChild))
            .Select(c => c.Clr);

        Assert.Empty(offenders);
    }

    [Fact]
    public void A_side_indexed_category_is_not_a_longtext_scan()
    {
        // the index interceptor runs inside every SaveChanges; stemming a document body would write
        // thousands of rows into the user's transaction
        var offenders = SearchCatalog.Categories
            .Where(c => c.Has(SearchTraits.SideIndexed) && c.Has(SearchTraits.Heavy))
            .Select(c => c.Clr);

        Assert.Empty(offenders);
    }

    [Fact]
    public void Index_orders_by_the_declared_sequence_and_sorts_the_unknown_last()
    {
        var first = SearchCatalog.Categories[0].Clr;
        var second = SearchCatalog.Categories[1].Clr;

        Assert.True(SearchCatalog.Index(first) < SearchCatalog.Index(second));
        Assert.Equal(int.MaxValue, SearchCatalog.Index("NoSuchCategory"));
    }

    [Fact]
    public void An_unknown_category_reads_as_a_neutral_German_word_never_a_CLR_name()
    {
        // a raw English type name in a result heading reads to the user as a record type that does not exist
        Assert.Equal("Eintrag", SearchCatalog.German("SomeInternalThing"));
        Assert.Equal("Eintrag", SearchCatalog.Plural("SomeInternalThing"));
    }

    [Fact]
    public void Route_of_an_unknown_category_is_null()
    {
        Assert.Null(SearchCatalog.Route("SomeInternalThing", "id"));
        Assert.False(SearchCatalog.IsRoutable("SomeInternalThing"));
    }

    [Fact]
    public void Clrs_filters_by_trait()
    {
        var quick = SearchCatalog.Clrs(SearchTraits.Quick);

        Assert.Contains(nameof(NOOSE_Website.Data.Entities.People.Person), quick);
        Assert.DoesNotContain(nameof(NOOSE_Website.Data.Entities.Common.Comment), quick);
    }

    // ---- catalog <-> provider registry ----

    [Fact]
    public void Every_registered_provider_names_a_catalog_category()
    {
        using var ctx = new SqliteTestContext();

        var stray = SearchTestHost.Providers(ctx)
            .Select(p => p.Category)
            .Where(c => SearchCatalog.Find(c) is null)
            .ToArray();

        Assert.Empty(stray);
    }

    [Fact]
    public void No_category_has_two_providers()
    {
        using var ctx = new SqliteTestContext();

        var duplicates = SearchTestHost.Providers(ctx)
            .GroupBy(p => p.Category, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        Assert.Empty(duplicates);
    }

    [Fact]
    public void A_category_a_provider_fills_declares_the_partner_access_the_provider_claims()
    {
        using var ctx = new SqliteTestContext();

        // the trait says "a partner may see this at all"; the provider says how. They must agree, or the facet
        // bar offers a partner a category their provider will always answer empty.
        foreach (var provider in SearchTestHost.Providers(ctx))
        {
            var reachable = provider.Partner != PartnerAccess.Never;
            Assert.Equal(SearchCatalog.Has(provider.Category, SearchTraits.Partner), reachable);
        }
    }

    [Fact]
    public void Every_assistant_category_has_a_provider()
    {
        using var ctx = new SqliteTestContext();
        var filled = SearchTestHost.Providers(ctx).Select(p => p.Category).ToHashSet(StringComparer.Ordinal);

        // offering the model a filter that nothing fills answers every such question with a false "no hits"
        var empty = SearchCatalog.Clrs(SearchTraits.Assistant).Where(c => !filled.Contains(c)).ToArray();

        Assert.Empty(empty);
    }

    [Fact]
    public void Every_side_indexed_category_has_a_provider_that_resolves_its_ids()
    {
        using var ctx = new SqliteTestContext();
        var filled = SearchTestHost.Providers(ctx).Select(p => p.Category).ToHashSet(StringComparer.Ordinal);

        // an indexed type without a provider writes index rows nothing can ever read back
        var unreachable = SearchCatalog.Clrs(SearchTraits.SideIndexed).Where(c => !filled.Contains(c)).ToArray();

        Assert.Empty(unreachable);
    }

    [Fact]
    public void Every_routable_category_produces_a_navigable_href()
    {
        foreach (var category in SearchCatalog.Categories.Where(c => c.RouteTemplate is not null))
        {
            var href = SearchNavigation.For(category.Clr, "the-id");

            Assert.NotNull(href);
            Assert.StartsWith("/", href);
            Assert.Contains("the-id", href);
        }
    }
}
