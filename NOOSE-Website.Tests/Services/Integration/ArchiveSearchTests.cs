using System.Security.Claims;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Search;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;
using NOOSE_Website.Services.Search;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>The default search hides the archive; the facet brings it back — in both waves.</summary>
public sealed class ArchiveSearchTests
{
    private static SearchService Svc(SqliteTestContext ctx) => SearchTestHost.NewService(ctx);

    private static ClaimsPrincipal Leader() => ClaimsPrincipalBuilder.Agent("l").WithRank(Rank.Director).Build();

    private static SearchCriteria Query(string text, bool includeArchived = false, bool fuzzy = false)
        => new() { Text = text, IncludeArchived = includeArchived, Fuzzy = fuzzy };

    private static SqliteTestContext Stock()
    {
        var ctx = new SqliteTestContext();
        using var db = ctx.NewContext();
        db.People.Add(Seed.Person("aktiv", "Meier Aktiv"));
        db.People.Add(Seed.Person("archiv", "Meier Archiv", p => p.IsArchived = true));
        db.SaveChanges();
        return ctx;
    }

    private static IReadOnlyList<SearchHit> People(SearchResults results)
        => results.Groups.FirstOrDefault(g => g.Category == nameof(Person))?.Hit ?? [];

    [Fact]
    public async Task The_default_search_leaves_the_archive_out()
    {
        using var ctx = Stock();
        var hits = People(await Svc(ctx).SearchAsync(Query("Meier"), Leader()));
        Assert.Contains(hits, h => h.TargetId == "aktiv");
        Assert.DoesNotContain(hits, h => h.TargetId == "archiv");
    }

    [Fact]
    public async Task The_facet_brings_the_archive_back()
    {
        using var ctx = Stock();
        var hits = People(await Svc(ctx).SearchAsync(Query("Meier", includeArchived: true), Leader()));
        Assert.Contains(hits, h => h.TargetId == "archiv");
    }

    [Fact]
    public async Task The_phonetic_second_wave_respects_the_facet()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            // "Mayr" only reaches the record through the phonetic side index, never through the substring pass
            db.People.Add(Seed.Person("archiv", "Mayr", p => p.IsArchived = true));
            db.SearchPhoneticKeys.Add(new SearchPhoneticKey
            {
                EntityType = nameof(Person), EntityId = "archiv", SourceId = "archiv", Key = ColognePhonetic.Encode("Mayr"),
            });
            db.SaveChanges();
        }

        var hidden = People(await Svc(ctx).SearchAsync(Query("Meier", fuzzy: true), Leader()));
        Assert.DoesNotContain(hidden, h => h.TargetId == "archiv");

        var shown = People(await Svc(ctx).SearchAsync(Query("Meier", includeArchived: true, fuzzy: true), Leader()));
        Assert.Contains(shown, h => h.TargetId == "archiv");
    }

    [Fact]
    public async Task The_command_palette_never_offers_an_archived_record()
    {
        using var ctx = Stock();
        var hits = await Svc(ctx).QuickSearchAsync("Meier", Leader());
        Assert.Contains(hits, h => h.TargetId == "aktiv");
        Assert.DoesNotContain(hits, h => h.TargetId == "archiv");
    }
}
