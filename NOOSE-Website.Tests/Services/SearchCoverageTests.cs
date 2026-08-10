using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Services.Search;

namespace NOOSE_Website.Tests.Services;

/// <summary>The guarantee behind "the search covers everything an agent may see".</summary>
/// <remarks>
/// Reflects over every <c>DbSet</c> on <see cref="AppDbContext"/>. An entity must either be a search category or
/// stand in <see cref="SearchCatalog.NotSearchable"/> with a reason. That turns the promise into a build failure
/// rather than a claim that was true on the day it was written — a new table now forces the author to decide.
/// </remarks>
public class SearchCoverageTests
{
    private static IReadOnlyList<string> EntityNames()
        => typeof(AppDbContext).GetProperties()
            .Where(p => p.PropertyType.IsGenericType
                && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(p => p.PropertyType.GetGenericArguments()[0].Name)
            // ASP.NET Identity's own tables are framework plumbing, never NOOSE records
            .Where(n => !n.StartsWith("Identity", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

    [Fact]
    public void Every_entity_is_either_searchable_or_excluded_with_a_reason()
    {
        var searchable = SearchCatalog.Categories.Select(c => c.Clr).ToHashSet(StringComparer.Ordinal);

        var undecided = EntityNames()
            .Where(n => !searchable.Contains(n) && !SearchCatalog.NotSearchable.ContainsKey(n))
            .ToArray();

        Assert.Empty(undecided);
    }

    [Fact]
    public void Every_exclusion_carries_a_reason()
    {
        var mute = SearchCatalog.NotSearchable
            .Where(e => string.IsNullOrWhiteSpace(e.Value))
            .Select(e => e.Key)
            .ToArray();

        Assert.Empty(mute);
    }

    [Fact]
    public void No_entity_is_both_searchable_and_excluded()
    {
        var searchable = SearchCatalog.Categories.Select(c => c.Clr).ToHashSet(StringComparer.Ordinal);

        var contradictory = SearchCatalog.NotSearchable.Keys.Where(searchable.Contains).ToArray();

        Assert.Empty(contradictory);
    }

    [Fact]
    public void No_exclusion_names_an_entity_that_no_longer_exists()
    {
        // a stale exclusion silently absolves the author of a table that was renamed
        var known = typeof(AppDbContext).Assembly.GetTypes()
            .Select(t => t.Name)
            .ToHashSet(StringComparer.Ordinal);

        var ghosts = SearchCatalog.NotSearchable.Keys.Where(k => !known.Contains(k)).ToArray();

        Assert.Empty(ghosts);
    }

    [Fact]
    public void The_agent_account_itself_is_a_search_category()
    {
        // Agent hangs off IdentityDbContext's Users, not a DbSet property of our own, so the reflection above
        // cannot see it — assert it separately rather than let personnel files fall through the guarantee
        Assert.NotNull(SearchCatalog.Find("Agent"));
    }
}
