using System.Security.Claims;
using System.Text.Json;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Llm;
using NOOSE_Website.Services;
using NOOSE_Website.Services.Llm.Tools;
using NOOSE_Website.Tests.Infrastructure;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>The brief tool renders a stored brief. Three of the seven fields it holds used to be generated at
/// quota cost and then never handed to the model — among them the whole connections list.</summary>
public sealed class NooseiBriefToolTests
{
    private static ClaimsPrincipal Leader()
        => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).Build();

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static DossierBrief FullBrief() => new(
        Tldr: "Kurzfassung der Akte.",
        Kernpunkte: ["Erster Kernpunkt"],
        EinstufungBewertung: "Die Einstufung passt.",
        Verbindungen: [new BriefConnection("Ballas", "Mitgliedschaft", "Führungsrolle")],
        Verlauf: [new BriefEvent("01.02.2026", "Festnahme in Vinewood")],
        OffenePunkte: ["Wohnanschrift unbekannt"],
        Risiko: new BriefRisk("hoch", "Bewaffnet"));

    [Fact]
    public async Task GetBrief_RendersEveryStoredField()
    {
        var dossier = Substitute.For<IDossierSummaryService>();
        dossier.GetAsync("Person", "p1", Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DossierSummaryView?>(
                new DossierSummaryView(true, true, FullBrief(), new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), false)));

        var result = await new GetBriefTool(dossier).InvokeAsync(
            Args("""{"typ":"Person","id":"p1"}"""), NooseiToolContext.From(Leader()));

        Assert.Contains("Kurzfassung der Akte.", result.Text);
        Assert.Contains("Erster Kernpunkt", result.Text);
        Assert.Contains("Die Einstufung passt.", result.Text);
        // the three that were generated, stored and then dropped on the way out
        Assert.Contains("Ballas (Mitgliedschaft) — Führungsrolle", result.Text);
        Assert.Contains("01.02.2026: Festnahme in Vinewood", result.Text);
        Assert.Contains("Wohnanschrift unbekannt", result.Text);
        Assert.Contains("Risiko: hoch", result.Text);
    }

    [Fact]
    public async Task GetBrief_SaysTheRecordChangedSinceTheBrief()
    {
        var dossier = Substitute.For<IDossierSummaryService>();
        dossier.GetAsync("Person", "p1", Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DossierSummaryView?>(
                new DossierSummaryView(true, true, FullBrief(), new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), true)));

        var result = await new GetBriefTool(dossier).InvokeAsync(
            Args("""{"typ":"Person","id":"p1"}"""), NooseiToolContext.From(Leader()));

        Assert.Contains("seit diesem Kurzbrief geändert", result.Text);
    }
}
