using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Services;
using NOOSE_Website.Services.Search;

namespace NOOSE_Website.Tests.Services;

/// <summary>Structural guard: a quick-capture note target must be a record that really shows comments.</summary>
/// <remarks>
/// <see cref="QuickCapture.CommentTargets"/> sits between two things that move on their own: a record grows a
/// comment section in a <c>.razor</c> file, and a category gains <see cref="SearchTraits.Quick"/> in the catalog.
/// There is no bUnit here, so nothing else holds the list against the markup - a target whose record shows no
/// comments would write the note and hide it for good, and a newly quick category would slip into the picker
/// without anyone deciding that it should. Both turn this red instead.
/// </remarks>
public sealed partial class CommentTargetScanTests
{
    /// <summary>Commentable, quick, and still kept out of the quick capture - the one standing exception.</summary>
    /// <remarks>
    /// The personnel file is gated to leadership by <c>Visibility</c> while the quick search hands it to everyone
    /// (<c>PersonnelSearchProviders.QuickAsync</c> filters on the codename alone), so the entry would promise nine
    /// agents out of ten something the service then refuses. Its own "Vermerk" is a different record
    /// (<c>AgentNote</c>) with a different form, so the one word would mean two things inside one dialog.
    /// </remarks>
    private static readonly IReadOnlySet<string> DeliberatelyOmitted =
        new HashSet<string>(StringComparer.Ordinal) { nameof(Agent) };

    [Fact]
    public void Every_quick_capture_target_has_a_comment_section()
    {
        var commentable = Commentable();

        // if this ever hits zero the guard has quietly stopped guarding anything
        Assert.NotEmpty(commentable);

        var missing = QuickCapture.CommentTargets
            .Where(t => !commentable.Contains(t))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(missing.Length == 0,
            "Die Schnellerfassung bietet Ziele an, die den Vermerk nirgends anzeigen - es gibt keinen "
            + "<CommentPanel>-Einbau für: " + string.Join(", ", missing));
    }

    [Fact]
    public void Only_the_personnel_file_is_commentable_quick_and_left_out()
    {
        var commentable = Commentable();

        // if this ever hits zero the guard has quietly stopped guarding anything
        Assert.NotEmpty(commentable);

        var reachable = commentable.Where(c => SearchCatalog.Has(c, SearchTraits.Quick)).ToArray();
        Assert.NotEmpty(reachable);

        var unexplained = reachable
            .Where(c => !QuickCapture.CommentTargets.Contains(c) && !DeliberatelyOmitted.Contains(c))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(unexplained.Length == 0,
            "Diese Typen tragen einen Kommentar-Abschnitt und den Quick-Trait, stehen aber weder in "
            + "QuickCapture.CommentTargets noch in der Ausnahmeliste dieses Tests - jemand muss entscheiden, "
            + "statt dass still ein neues Schnellerfassungs-Ziel entsteht: " + string.Join(", ", unexplained));

        var stale = DeliberatelyOmitted
            .Where(c => !reachable.Contains(c, StringComparer.Ordinal)
                || QuickCapture.CommentTargets.Contains(c))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(stale.Length == 0,
            "Die Ausnahmeliste dieses Tests nennt Typen, die gar keine Ausnahme mehr sind - sie sind entweder "
            + "nicht mehr kommentierbar bzw. nicht mehr quick-auffindbar oder inzwischen selbst ein Ziel: "
            + string.Join(", ", stale));
    }

    /// <summary>CLR type names that carry a comment section somewhere in the markup.</summary>
    private static HashSet<string> Commentable()
    {
        var root = SourceRoot();
        Assert.True(Directory.Exists(root), $"Quellordner nicht gefunden: {root}");

        return Directory
            .EnumerateFiles(root, "*.razor", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(f => CommentPanel().Matches(File.ReadAllText(f)))
            .Select(m => m.Groups["clr"].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>A comment section and the record type it is wired to.</summary>
    [GeneratedRegex(@"<CommentPanel\b[^>]*?EntityType=""@nameof\((?<clr>[A-Za-z0-9_]+)\)""", RegexOptions.Singleline)]
    private static partial Regex CommentPanel();

    private static string SourceRoot([CallerFilePath] string here = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "NOOSE-Website"));
}
