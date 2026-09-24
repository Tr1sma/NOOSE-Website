using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NOOSE_Website.Services;
using NOOSE_Website.Services.Search;

namespace NOOSE_Website.Tests.Services;

/// <summary>Which record types a batch write from the search page may touch.</summary>
public sealed class RecordBatchTests
{
    private static string SourceRoot([CallerFilePath] string here = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "NOOSE-Website"));

    /// <summary>Types a component is placed for, as <c>EntityType="@nameof(X)"</c> on any page.</summary>
    private static HashSet<string> TypesCarrying(string component)
    {
        var root = SourceRoot();
        Assert.True(Directory.Exists(root), $"Quellordner nicht gefunden: {root}");
        var pattern = new Regex($"<{component}\\b[^>]*EntityType=\"@nameof\\((\\w+)\\)\"");
        return Directory.EnumerateFiles(Path.Combine(root, "Components"), "*.razor", SearchOption.AllDirectories)
            .SelectMany(f => pattern.Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);
    }

    [Fact]
    public void Linkable_types_are_link_types_with_a_page_and_a_record_hit()
    {
        Assert.Subset(LinkService.KnownTypes.ToHashSet(), RecordBatch.Linkable.ToHashSet());
        Assert.All(RecordBatch.Linkable, t =>
        {
            Assert.Equal(SearchHitShape.Record, SearchCatalog.Shape(t));
            Assert.True(SearchCatalog.IsRoutable(t), t);
        });
        // their hits point at the parent, so they can never be picked
        Assert.DoesNotContain("PersonDoc", RecordBatch.Linkable);
        Assert.DoesNotContain("Observation", RecordBatch.Linkable);
        Assert.DoesNotContain("MeetingAgendaItem", RecordBatch.Linkable);
    }

    [Fact]
    public void Anchors_are_records_that_list_their_links()
    {
        Assert.Subset(RecordBatch.Linkable.ToHashSet(), RecordBatch.LinkAnchors.ToHashSet());
        // conflicts and alliances only, no link list, or a takeover meaning
        foreach (var never in new[] { "Faction", "PersonGroup", "Party", "Document", "Agent", "Ticket", "Hinweis" })
        {
            Assert.DoesNotContain(never, RecordBatch.LinkAnchors);
        }
    }

    [Fact]
    public void Operation_and_taskforce_take_only_what_their_own_page_takes()
    {
        Assert.Equal(LinkService.InvolvedTypes.Order(), RecordBatch.LinkTargetsFor("Operation").Order());
        Assert.Equal(LinkService.InvolvedTypes.Order(), RecordBatch.LinkTargetsFor("Taskforce").Order());
        Assert.Equal(RecordBatch.Linkable.Order(), RecordBatch.LinkTargetsFor("Case").Order());
        Assert.Empty(RecordBatch.LinkTargetsFor("Faction"));
        Assert.Empty(RecordBatch.LinkTargetsFor("Hinweis"));
    }

    [Fact]
    public void Both_involvement_panels_read_the_shared_list()
    {
        var root = SourceRoot();
        foreach (var panel in new[]
                 {
                     Path.Combine(root, "Components", "Pages", "Operations", "Shared", "OperationInvolvedPanel.razor"),
                     Path.Combine(root, "Components", "Pages", "Taskforces", "Shared", "TaskforceRelationsPanel.razor"),
                 })
        {
            Assert.Contains("LinkService.InvolvedTypes", File.ReadAllText(panel), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Taggable_is_what_the_tag_filter_finds_again()
    {
        Assert.Equal(SearchCatalog.Clrs(SearchTraits.Tagged).Order(), RecordBatch.Taggable.Order());
        // and every one of them can show its tags on its own page
        Assert.Subset(TypesCarrying("TagChips"), RecordBatch.Taggable.ToHashSet());
    }

    [Fact]
    public void Followable_is_what_carries_a_follow_button()
    {
        Assert.Equal(TypesCarrying("FollowButton").Order(), RecordBatch.Followable.Order());
    }

    [Fact]
    public void Normalize_drops_blanks_and_doubles()
    {
        var result = RecordBatch.Normalize([("Person", "p1"), ("Person", "p1"), ("", "x"), ("Person", " "), ("Case", "p1")]);

        Assert.Equal([("Person", "p1"), ("Case", "p1")], result);
    }

    [Fact]
    public void Normalize_refuses_more_than_the_cap()
    {
        var many = Enumerable.Range(0, RecordBatch.Max + 1).Select(i => ("Person", $"p{i}"));

        Assert.Throws<InvalidOperationException>(() => RecordBatch.Normalize(many));
        Assert.Equal(RecordBatch.Max, RecordBatch.Normalize(many.Take(RecordBatch.Max)).Count);
    }
}
