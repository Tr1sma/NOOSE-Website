using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace NOOSE_Website.Tests.Services;

/// <summary>Structural guard for every list that offers "Diese Ansicht merken".</summary>
/// <remarks>
/// The button and the page it sits on are Razor, and no test renders Razor here. What makes a saved view work is
/// four promises the page keeps: it files its views under its own route, it saves exactly what it writes to the
/// address, it re-reads its filters when a view lands on it, and only its newest load may write the rows - a view
/// can land while a load still runs, and an older load finishing last would show rows the filters do not describe.
/// A new list that forgets one of them turns this red instead of shipping a button that saves the wrong thing.
/// </remarks>
public sealed class SavedViewPageScanTests
{
    private static string SourceRoot([CallerFilePath] string here = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "NOOSE-Website"));

    private static List<(string Name, string Text)> ListsWithTheButton()
    {
        var root = SourceRoot();
        Assert.True(Directory.Exists(root), $"Quellordner nicht gefunden: {root}");
        return Directory
            .EnumerateFiles(Path.Combine(root, "Components", "Pages"), "*.razor", SearchOption.AllDirectories)
            .Select(f => (Name: Path.GetRelativePath(root, f), Text: File.ReadAllText(f)))
            .Where(f => f.Text.Contains("<SavedViewButton", StringComparison.Ordinal))
            .ToList();
    }

    [Fact]
    public void The_six_lists_carry_the_button()
    {
        // if this ever drops the guards below have quietly stopped guarding anything
        var names = ListsWithTheButton().Select(l => Path.GetFileName(l.Name)).Order().ToArray();

        Assert.Equal(["CasesList.razor", "FactionsList.razor", "JobsList.razor", "NotificationInbox.razor", "OperationsList.razor",
            "PeopleList.razor"],
            names);
    }

    [Fact]
    public void Every_list_files_its_views_under_its_own_route()
    {
        var offenders = ListsWithTheButton()
            .Where(l =>
            {
                var page = Regex.Match(l.Text, "^@page \"([^\"]+)\"", RegexOptions.Multiline).Groups[1].Value;
                var base_ = Regex.Match(l.Text, "<SavedViewButton[^>]*BaseRoute=\"([^\"]+)\"").Groups[1].Value;
                return page.Length == 0 || !string.Equals(page, base_, StringComparison.Ordinal);
            })
            .Select(l => l.Name)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Every_list_saves_exactly_what_it_writes_to_the_address()
    {
        // one tuple list for both: a view built from a second copy would drift the first time a filter is added
        var offenders = ListsWithTheButton()
            .Where(l => !Regex.IsMatch(l.Text, "<SavedViewButton[^>]*Filters=\"FilterState\"")
                || !l.Text.Contains("QueryState.WriteAsync(JS, Nav, FilterState)", StringComparison.Ordinal))
            .Select(l => l.Name)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Every_list_re_reads_its_filters_when_a_view_lands_on_it()
    {
        var offenders = ListsWithTheButton()
            .Where(l => !Regex.IsMatch(l.Text, "<SavedViewButton[^>]*OnApply=\"ApplyViewAsync\"")
                || !l.Text.Contains("private async Task ApplyViewAsync(string query)", StringComparison.Ordinal)
                || !l.Text.Contains("ReadFilters(query);", StringComparison.Ordinal)
                // the first read comes from the address the page was opened with, through the same method
                || !l.Text.Contains("ReadFilters(new Uri(Nav.Uri).Query);", StringComparison.Ordinal))
            .Select(l => l.Name)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Every_list_lets_only_its_newest_load_write_the_rows()
    {
        var offenders = ListsWithTheButton()
            .Where(l =>
            {
                var load = Regex.Match(l.Text, @"private async Task LoadAsync\(\)\s*\{(.*?)\n    \}", RegexOptions.Singleline);
                if (!load.Success)
                {
                    return true;
                }
                var body = load.Groups[1].Value;
                var stamp = body.IndexOf("var generation = ++_loadGeneration;", StringComparison.Ordinal);
                var check = body.IndexOf("if (generation != _loadGeneration)", StringComparison.Ordinal);
                var lastAwait = body.LastIndexOf("await ", StringComparison.Ordinal);
                // stamped before the first await, checked after the last one
                return stamp < 0 || check < 0 || stamp > body.IndexOf("await ", StringComparison.Ordinal) || check < lastAwait;
            })
            .Select(l => l.Name)
            .ToArray();

        Assert.Empty(offenders);
    }
}
