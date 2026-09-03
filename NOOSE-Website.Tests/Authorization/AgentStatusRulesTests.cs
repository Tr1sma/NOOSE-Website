using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Authorization;

/// <summary>Guards the one rule the login endpoint and the circuit revalidation must agree on.</summary>
/// <remarks>
/// They disagreed once: the endpoint signed in Applicant and Civilian, the revalidation accepted only Active, and
/// every citizen circuit was force-signed-out 30 seconds after arriving. A drift here is not a compile error, so it
/// is a scan.
/// </remarks>
public class AgentStatusRulesTests
{
    [Theory]
    [InlineData(AgentStatus.Active)]
    [InlineData(AgentStatus.Applicant)]
    [InlineData(AgentStatus.Civilian)]
    public void AnAuthenticatedAccountKeepsItsSession(AgentStatus status)
        => Assert.True(AgentStatusRules.MayHoldSession(status));

    [Theory]
    [InlineData(AgentStatus.Pending)]
    [InlineData(AgentStatus.Blocked)]
    [InlineData(AgentStatus.Terminated)]
    public void AnAccountWithoutAccessIsEvicted(AgentStatus status)
        => Assert.False(AgentStatusRules.MayHoldSession(status));

    [Fact]
    public void EveryStatusIsDecided()
    {
        var undecided = Enum.GetValues<AgentStatus>()
            .Where(s => s is not (AgentStatus.Active or AgentStatus.Applicant or AgentStatus.Civilian
                or AgentStatus.Pending or AgentStatus.Blocked or AgentStatus.Terminated))
            .ToList();
        Assert.True(undecided.Count == 0,
            "New AgentStatus without a session decision: " + string.Join(", ", undecided));
    }

    [Fact]
    public void TheLoginEndpointSignsInExactlyTheStatusesThatMayHoldASession()
    {
        var source = File.ReadAllText(EndpointFile());
        var signedIn = Regex.Matches(source, @"case AgentStatus\.(\w+):(?<body>(?:(?!\bcase\b|\bdefault:).)*)",
                RegexOptions.Singleline)
            .Where(m => m.Groups["body"].Value.Contains("SignInAsync", StringComparison.Ordinal))
            .Select(m => Enum.Parse<AgentStatus>(m.Groups[1].Value))
            .OrderBy(s => s)
            .ToList();

        var allowed = Enum.GetValues<AgentStatus>()
            .Where(AgentStatusRules.MayHoldSession)
            .OrderBy(s => s)
            .ToList();

        Assert.Equal(allowed, signedIn);
    }

    private static string EndpointFile([CallerFilePath] string here = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..",
            "NOOSE-Website", "Components", "Account", "IdentityComponentsEndpointRouteBuilderExtensions.cs"));
}
