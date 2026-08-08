using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Search;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;
using NOOSE_Website.Tests.Infrastructure;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Integration tests for the phonetic/stem side-index path in <see cref="SearchService"/>.
/// Uses „Mayr" vs query „Meier" — edit distance 3 (Levenshtein misses it), same Cologne code (side-index catches it).</summary>
public sealed class SearchServiceFuzzyTests
{
    private static SearchService Svc(SqliteTestContext ctx) => new(ctx.Factory);
    private static ViewerScope Junior() => ViewerScope.From(ClaimsPrincipalBuilder.Agent("j").WithRank(Rank.JuniorAgent).Build());
    private static ViewerScope Leader() => ViewerScope.From(ClaimsPrincipalBuilder.Agent("l").WithRank(Rank.Director).Build());

    private static SearchCriteria Fuzzy(string text) => new() { Text = text, Fuzzy = true };

    [Fact]
    public async Task Fuzzy_PhoneticSideIndex_FindsSoundalikeBeyondLevenshtein()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person("p1", "Mayr"));
            db.SearchPhoneticKeys.Add(new SearchPhoneticKey
            {
                EntityType = nameof(Person), EntityId = "p1", SourceId = "p1", Key = ColognePhonetic.Encode("Mayr"),
            });
            db.SaveChanges();
        }

        var groups = await Svc(ctx).SearchAsync(Fuzzy("Meier"), Junior());

        var people = groups.FirstOrDefault(g => g.Category == nameof(Person));
        Assert.NotNull(people);
        Assert.Contains(people!.Hit, h => h.TargetId == "p1");
    }

    [Fact]
    public async Task Fuzzy_PhoneticSideIndex_HidesClassifiedFromNonLeadership()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person("p1", "Mayr", p => p.IsClassified = true));
            db.SearchPhoneticKeys.Add(new SearchPhoneticKey
            {
                EntityType = nameof(Person), EntityId = "p1", SourceId = "p1", Key = ColognePhonetic.Encode("Mayr"),
            });
            db.SaveChanges();
        }

        var asJunior = await Svc(ctx).SearchAsync(Fuzzy("Meier"), Junior());
        Assert.DoesNotContain(asJunior.SelectMany(g => g.Hit), h => h.TargetId == "p1");

        var asLeader = await Svc(ctx).SearchAsync(Fuzzy("Meier"), Leader());
        Assert.Contains(asLeader.SelectMany(g => g.Hit), h => h.TargetId == "p1");
    }
}
