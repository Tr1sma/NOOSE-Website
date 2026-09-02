using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace NOOSE_Website.Tests.Services.Public;

/// <summary>
/// Pins the two properties of the request pipeline that no other test can see: the order of the load-bearing
/// middleware pairs, and that the rate limiter is partitioned per caller rather than site-wide.
/// </summary>
/// <remarks>
/// <para>
/// There is no HTTP-level test in this repository, so middleware order is verified nowhere - and it is
/// load-bearing twice over. The defect this pins: <c>UseRateLimiter</c> ran BEFORE <c>UseAntiforgery</c>, so a
/// POST without a token spent a permit; combined with a single site-wide bucket, ten anonymous requests a minute
/// held the login route at 429 for every agent, citizen and applicant at once.
/// </para>
/// <para>
/// A source scan is a weaker instrument than a running host, and it is honest about that: it proves the calls
/// appear in this order in <c>Program.cs</c>, not that a request travels through them. What it does catch is the
/// edit that moves one line - which is exactly how the defect arose.
/// </para>
/// </remarks>
public partial class PipelineOrderTests
{
    private static string ProgramSource([CallerFilePath] string here = "")
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..",
            "NOOSE-Website"));
        var path = Path.Combine(root, "Program.cs");
        Assert.True(File.Exists(path), $"Program.cs nicht gefunden: {path}");
        return File.ReadAllText(path);
    }

    /// <summary>The prose that explains why a shape is wrong contains the very shape.</summary>
    private static string WithoutComments(string text)
        => LineComment().Replace(BlockComment().Replace(text, " "), " ");

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex BlockComment();

    [GeneratedRegex(@"//.*")]
    private static partial Regex LineComment();

    /// <summary>Pipeline calls that must appear in this order, each with the reason it matters.</summary>
    private static readonly (string Earlier, string Later, string Why)[] Ordered =
    [
        ("app.UseForwardedHeaders()", "app.UseAuthentication()",
            "ohne die echte Client-IP partitioniert der Limiter auf die nginx-Adresse"),
        ("app.UseMiddleware<NOOSE_Website.Infrastructure.PublicIndexingMiddleware>()",
            "app.UseStatusCodePagesWithReExecute",
            "re-executete Fehlerseiten sollen den noindex-Header behalten"),
        ("app.UseAuthentication()", "app.UseAuthorization()",
            "Autorisierung braucht ein aufgelöstes Principal"),
        ("app.UseAuthorization()", "app.UseAntiforgery()", "Hausreihenfolge"),
        ("app.UseAntiforgery()", "app.UseRateLimiter()",
            "ein POST ohne Token darf kein Permit verbrauchen, sonst hält ein anonymer Besucher die "
            + "Anmeldung dauerhaft auf 429"),
        ("app.UseRateLimiter()", "app.MapRazorComponents<App>()",
            "die öffentliche Suche ist eine Razor-Route und wird nur vom GlobalLimiter erreicht"),
    ];

    [Fact]
    public void TheLoadBearingMiddlewarePairsAreInOrder()
    {
        var source = ProgramSource();
        foreach (var (earlier, later, why) in Ordered)
        {
            var first = source.IndexOf(earlier, StringComparison.Ordinal);
            var second = source.IndexOf(later, StringComparison.Ordinal);
            Assert.True(first >= 0, $"Pipeline-Aufruf nicht gefunden: {earlier}");
            Assert.True(second >= 0, $"Pipeline-Aufruf nicht gefunden: {later}");
            Assert.True(first < second, $"{earlier} muss vor {later} stehen: {why}");
        }
    }

    [Fact]
    public void TheRateLimiterIsPartitionedPerCaller()
    {
        // comments stripped: the line that explains why AddFixedWindowLimiter is wrong names it
        var source = WithoutComments(ProgramSource());

        // AddFixedWindowLimiter creates exactly ONE partition for the whole site: a shared budget, not a
        // per-visitor one. The partitioned form is the only correct one here.
        Assert.DoesNotContain("AddFixedWindowLimiter", source, StringComparison.Ordinal);
        Assert.Contains("RateLimitPartition.GetFixedWindowLimiter", source, StringComparison.Ordinal);
        Assert.Contains("RemoteIpAddress", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ARejectedRequestAnswersWithABody()
    {
        // a bodyless 429 is re-executed by UseStatusCodePagesWithReExecute, which would tell the locked-out
        // visitor that the login route does not exist
        var source = ProgramSource();
        Assert.Contains("options.OnRejected", source, StringComparison.Ordinal);
        Assert.Contains("Response.WriteAsync", source, StringComparison.Ordinal);
        Assert.Contains("MetadataName.RetryAfter", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePublicSearchIsTheOnlyGloballyLimitedPath()
    {
        // it is a Razor route and carries no endpoint metadata a named policy could attach to, so it has to be
        // gated in the GlobalLimiter - and everything else, above all /_blazor and /_framework, must not be
        var source = ProgramSource();
        Assert.Contains("options.GlobalLimiter", source, StringComparison.Ordinal);
        Assert.Contains("/suche-oeffentlich", source, StringComparison.Ordinal);
        Assert.Contains("RateLimitPartition.GetNoLimiter<string>", source, StringComparison.Ordinal);
    }
}
