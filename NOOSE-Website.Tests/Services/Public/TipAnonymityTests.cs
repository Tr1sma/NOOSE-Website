using System.Runtime.CompilerServices;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Public;
using NOOSE_Website.Services.Public;

namespace NOOSE_Website.Tests.Services.Public;

/// <summary>The tip anonymity promise, held structurally rather than by care during review.</summary>
/// <remarks>
/// Two halves. The rule itself decides where a tip's audit actor may be resolved, and both record-facing read paths
/// have to name it — the change protocol deliberately does not, because that surface is the abuse control. The second
/// half is a marker scan over the two pages a non-agent renders.
/// </remarks>
public class TipAnonymityTests
{
    private static string Root([CallerFilePath] string here = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..", "NOOSE-Website"));

    private static string Read(params string[] parts)
    {
        var file = Path.Combine(new[] { Root() }.Concat(parts).ToArray());
        Assert.True(File.Exists(file), $"Datei nicht gefunden: {file}");
        return File.ReadAllText(file);
    }

    [Fact]
    public void The_rule_covers_the_tip_and_its_messages_and_nothing_else()
    {
        Assert.True(TipAnonymity.HidesActor(nameof(Hinweis)));
        Assert.True(TipAnonymity.HidesActor(nameof(HinweisNachricht)));

        Assert.False(TipAnonymity.HidesActor(nameof(OeffentlicheFahndung)));
        Assert.False(TipAnonymity.HidesActor("Person"));
        Assert.False(TipAnonymity.HidesActor(null));
    }

    [Fact]
    public void Both_record_facing_read_paths_ask_the_rule()
    {
        // an agent may report through his civilian identity; naming him on the file he reported about is the leak
        Assert.Contains("TipAnonymity.HidesActor", Read("Services", "TimelineService.cs"), StringComparison.Ordinal);
        Assert.Contains("TipAnonymity.HidesActor", Read("Services", "GlobalChronikService.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void The_change_protocol_deliberately_does_not()
    {
        // /nachweis is where the submitting account is supposed to be visible; that is the abuse control
        Assert.DoesNotContain("TipAnonymity", Read("Services", "AuditLogQueryService.cs"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Codename")]
    [InlineData("AuthorAgentId")]
    [InlineData("HandlerId")]
    [InlineData("HandlerCodename")]
    [InlineData("GetAgentId")]
    public void No_citizen_facing_page_names_an_agent(string marker)
    {
        var pages = new[]
        {
            Read("Components", "Pages", "Public", "TipForm.razor"),
            Read("Components", "Pages", "Portal", "MeineHinweise.razor"),
        };

        Assert.All(pages, page => Assert.DoesNotContain(marker, page, StringComparison.Ordinal));
    }

    [Fact]
    public void The_citizen_facing_records_carry_no_agent_and_no_row_id()
    {
        // PublicWantedModelTests holds the same line for the wanted records; these three are the tip's half
        var types = new[] { typeof(CitizenTipRow), typeof(CitizenTipDetail), typeof(CitizenTipMessage) };
        var names = types.SelectMany(t => t.GetProperties().Select(p => $"{t.Name}.{p.Name}")).ToArray();

        Assert.DoesNotContain(names, n => n.EndsWith(".Id", StringComparison.Ordinal));
        Assert.DoesNotContain(names, n => n.Contains("Author", StringComparison.Ordinal)
            || n.Contains("Handler", StringComparison.Ordinal)
            || n.Contains("Codename", StringComparison.Ordinal)
            || n.Contains("AgentId", StringComparison.Ordinal));
    }
}
