using NOOSE_Website.Data.Entities.Evidence;
using NOOSE_Website.Models.Evidence;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

/// <summary>Category grouping, tri-state and scope wording of the clearing dialog.</summary>
public class EvidenceClearingGroupsTests
{
    private static EvidenceItemDisplay Row(string id, string? category, int onHand = 1)
        => new(new EvidenceItem { Id = id, Name = id, Category = category }, onHand);

    private static IReadOnlySet<string> Selected(params string[] ids)
        => ids.ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void Build_groups_a_category_case_insensitively()
    {
        var groups = EvidenceClearingGroups.Build([Row("a", "Waffen"), Row("b", "waffen")]);

        var group = Assert.Single(groups);
        Assert.Equal(2, group.Rows.Count);
    }

    [Fact]
    public void Build_labels_a_group_with_the_first_spelling_seen()
    {
        var groups = EvidenceClearingGroups.Build([Row("a", "WAFFEN"), Row("b", "waffen")]);

        Assert.Equal("WAFFEN", Assert.Single(groups).Label);
    }

    [Fact]
    public void Build_pools_null_empty_and_blank_into_the_uncategorised_group()
    {
        var groups = EvidenceClearingGroups.Build([Row("a", null), Row("b", ""), Row("c", "   ")]);

        var group = Assert.Single(groups);
        Assert.Equal(EvidenceCategories.None, group.Key);
        Assert.Equal("Ohne Kategorie", group.Label);
        Assert.Equal(3, group.Rows.Count);
    }

    [Fact]
    public void Build_sorts_named_groups_alphabetically_and_uncategorised_last()
    {
        var groups = EvidenceClearingGroups.Build(
            [Row("a", null), Row("b", "Waffen"), Row("c", "Drogen"), Row("d", "Ausrüstung")]);

        Assert.Equal(["Ausrüstung", "Drogen", "Waffen", "Ohne Kategorie"], groups.Select(g => g.Label));
    }

    [Fact]
    public void Build_of_nothing_is_empty()
    {
        Assert.Empty(EvidenceClearingGroups.Build([]));
    }

    [Fact]
    public void State_is_true_when_every_row_is_ticked()
    {
        var rows = new List<EvidenceItemDisplay> { Row("a", "Waffen"), Row("b", "Waffen") };

        Assert.True(EvidenceClearingGroups.State(rows, Selected("a", "b")));
    }

    [Fact]
    public void State_is_false_when_no_row_is_ticked()
    {
        var rows = new List<EvidenceItemDisplay> { Row("a", "Waffen"), Row("b", "Waffen") };

        Assert.False(EvidenceClearingGroups.State(rows, Selected("c")));
    }

    [Fact]
    public void State_is_indeterminate_when_only_some_rows_are_ticked()
    {
        var rows = new List<EvidenceItemDisplay> { Row("a", "Waffen"), Row("b", "Waffen") };

        Assert.Null(EvidenceClearingGroups.State(rows, Selected("a")));
    }

    [Fact]
    public void State_of_no_rows_is_false_so_a_filtered_group_never_reads_as_full()
    {
        Assert.False(EvidenceClearingGroups.State([], Selected("a")));
    }

    [Fact]
    public void ScopeLabel_names_the_whole_chamber_when_every_group_is_full()
    {
        var groups = EvidenceClearingGroups.Build([Row("a", "Waffen"), Row("b", "Drogen")]);

        Assert.Equal("Kammer geräumt", EvidenceClearingGroups.ScopeLabel(groups, Selected("a", "b")));
    }

    [Fact]
    public void ScopeLabel_names_a_single_full_category()
    {
        var groups = EvidenceClearingGroups.Build([Row("a", "Waffen"), Row("b", "Drogen")]);

        Assert.Equal("„Waffen“ geräumt", EvidenceClearingGroups.ScopeLabel(groups, Selected("a")));
    }

    [Fact]
    public void ScopeLabel_counts_several_full_categories()
    {
        var groups = EvidenceClearingGroups.Build(
            [Row("a", "Waffen"), Row("b", "Drogen"), Row("c", "Geld")]);

        Assert.Equal("2 Kategorien geräumt", EvidenceClearingGroups.ScopeLabel(groups, Selected("a", "b")));
    }

    [Fact]
    public void ScopeLabel_stays_generic_when_a_category_is_only_half_ticked()
    {
        var groups = EvidenceClearingGroups.Build(
            [Row("a", "Waffen"), Row("b", "Waffen"), Row("c", "Drogen")]);

        // "„Drogen“ geräumt" would hide that half of the weapons went too
        Assert.Equal("Auswahl geräumt", EvidenceClearingGroups.ScopeLabel(groups, Selected("a", "c")));
    }

    [Fact]
    public void ScopeLabel_stays_generic_when_nothing_is_ticked()
    {
        var groups = EvidenceClearingGroups.Build([Row("a", "Waffen")]);

        Assert.Equal("Auswahl geräumt", EvidenceClearingGroups.ScopeLabel(groups, Selected()));
    }

    [Fact]
    public void ScopeLabel_names_the_uncategorised_group_by_its_label()
    {
        var groups = EvidenceClearingGroups.Build([Row("a", null), Row("b", "Waffen")]);

        Assert.Equal("„Ohne Kategorie“ geräumt", EvidenceClearingGroups.ScopeLabel(groups, Selected("a")));
    }
}
