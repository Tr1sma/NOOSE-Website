using NOOSE_Website.Services.Llm.Tools;
using NOOSE_Website.Services.Search;
using Xunit;

namespace NOOSE_Website.Tests.Services;

/// <summary>Every search category the asking agent can reach must also be reachable by NOOSEI — as a record it can
/// open, a content section, an operating area, or a personal tool — or be excluded on purpose with a reason. This is
/// the standing guarantee that the assistant reads everything the agent can; a new category makes the build red until
/// someone decides where it belongs. Modelled on <see cref="SearchCoverageTests" />.</summary>
public class NooseiCoverageTests
{
    private static IReadOnlyList<string> Categories()
        => SearchCatalog.Categories.Select(c => c.Clr).ToList();

    private static bool Reachable(string clr)
        => NooseiRecordTypes.Can(clr, NooseiUse.Read)
            || NooseiRecordTypes.ReachableWithoutRead.ContainsKey(clr);

    [Fact]
    public void Every_search_category_is_reachable_or_excluded_with_a_reason()
    {
        var undecided = Categories()
            .Where(c => !Reachable(c) && !NooseiRecordTypes.NotAssistantReadable.ContainsKey(c))
            .ToArray();
        Assert.Empty(undecided);
    }

    [Fact]
    public void No_category_is_both_reachable_and_excluded()
    {
        var conflict = Categories()
            .Where(c => Reachable(c) && NooseiRecordTypes.NotAssistantReadable.ContainsKey(c))
            .ToArray();
        Assert.Empty(conflict);
    }

    [Fact]
    public void Every_exclusion_carries_a_reason()
    {
        var blank = NooseiRecordTypes.NotAssistantReadable
            .Where(e => string.IsNullOrWhiteSpace(e.Value)).Select(e => e.Key).ToArray();
        Assert.Empty(blank);
    }

    [Fact]
    public void Every_reachable_without_read_entry_names_its_path()
    {
        var blank = NooseiRecordTypes.ReachableWithoutRead
            .Where(e => string.IsNullOrWhiteSpace(e.Value)).Select(e => e.Key).ToArray();
        Assert.Empty(blank);
    }

    [Fact]
    public void Every_map_key_names_a_real_search_category()
    {
        var known = Categories().ToHashSet(StringComparer.Ordinal);
        var stray = NooseiRecordTypes.ReachableWithoutRead.Keys
            .Concat(NooseiRecordTypes.NotAssistantReadable.Keys)
            .Where(k => !known.Contains(k)).ToArray();
        Assert.Empty(stray);
    }

    [Fact]
    public void A_read_capable_category_is_not_also_listed_as_reachable_without_read()
    {
        // ReachableWithoutRead is for the non-Read categories; a Read type there is a contradiction, not a fallback
        var redundant = NooseiRecordTypes.ReachableWithoutRead.Keys
            .Where(k => NooseiRecordTypes.Can(k, NooseiUse.Read)).ToArray();
        Assert.Empty(redundant);
    }
}
